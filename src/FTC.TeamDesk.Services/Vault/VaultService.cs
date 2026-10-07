using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.Security;

namespace FTC.TeamDesk.Services;

/// <summary>
/// Password Vault. Master password -> PBKDF2-SHA512 -> AES-256 key held only in memory while unlocked.
/// Every record (title, username, password, notes...) is one AES-GCM blob; the database never sees plaintext.
/// The vault always starts locked: the key is never persisted anywhere.
/// </summary>
public sealed class VaultService : IVaultService, IDisposable
{
    private static readonly byte[] VerifierPlaintext = Encoding.UTF8.GetBytes("FTC-TEAMDESK-VAULT-V1");
    private const int MinMasterPasswordLength = 10;

    private readonly IVaultRepository _repo;
    private readonly IVaultCrypto _crypto;
    private readonly ISettingsService _settings;
    private readonly IActivityLogService _log;
    private readonly IClock _clock;
    private readonly object _gate = new();
    private byte[]? _key;
    private Timer? _autoLock;
    private VaultMeta? _meta;

    public VaultService(IVaultRepository repo, IVaultCrypto crypto, ISettingsService settings, IActivityLogService log, IClock clock)
    {
        _repo = repo; _crypto = crypto; _settings = settings; _log = log; _clock = clock;
    }

    public bool IsInitialized => _meta is not null;
    public bool IsUnlocked { get { lock (_gate) return _key is not null; } }
    public event EventHandler? StateChanged;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _meta = await _repo.GetMetaAsync(ct).ConfigureAwait(false);
    }

    public async Task CreateAsync(string masterPassword, CancellationToken ct = default)
    {
        if (IsInitialized) throw new DomainException("Vault.AlreadyExists");
        if (string.IsNullOrEmpty(masterPassword) || masterPassword.Length < MinMasterPasswordLength)
            throw new DomainException("Vault.PasswordTooShort");

        var salt = _crypto.GenerateSalt();
        var iterations = _crypto.DefaultIterations;
        var key = await Task.Run(() => _crypto.DeriveKey(masterPassword, salt, iterations), ct).ConfigureAwait(false);
        var meta = new VaultMeta { Salt = salt, Iterations = iterations, Verifier = _crypto.Encrypt(key, VerifierPlaintext) };
        await _repo.SaveMetaAsync(meta, ct).ConfigureAwait(false);
        _meta = meta;
        SetUnlocked(key);
        await _log.LogAsync(ActivityActions.VaultCreated, string.Empty, ActivityActor.User, ct).ConfigureAwait(false);
    }

    public async Task<VaultUnlockResult> UnlockAsync(string masterPassword, CancellationToken ct = default)
    {
        var meta = _meta ?? await _repo.GetMetaAsync(ct).ConfigureAwait(false);
        if (meta is null) return new VaultUnlockResult(VaultUnlockOutcome.NotInitialized);
        _meta = meta;

        var now = _clock.UtcNow;
        if (meta.LockedUntil is { } until && until > now)
            return new VaultUnlockResult(VaultUnlockOutcome.LockedOut, until - now);

        byte[] key;
        try { key = await Task.Run(() => _crypto.DeriveKey(masterPassword, meta.Salt, meta.Iterations), ct).ConfigureAwait(false); }
        catch (ArgumentException) { return await RegisterFailureAsync(meta, ct).ConfigureAwait(false); }

        if (VerifyKey(key, meta))
        {
            meta.FailedAttempts = 0;
            meta.LockedUntil = null;
            await _repo.SaveMetaAsync(meta, ct).ConfigureAwait(false);
            SetUnlocked(key);
            await _log.LogAsync(ActivityActions.VaultUnlocked, string.Empty, ActivityActor.User, ct).ConfigureAwait(false);
            return new VaultUnlockResult(VaultUnlockOutcome.Success);
        }

        CryptographicOperations.ZeroMemory(key);
        return await RegisterFailureAsync(meta, ct).ConfigureAwait(false);
    }

    public void Lock(string reason = "manual")
    {
        bool wasUnlocked;
        lock (_gate)
        {
            wasUnlocked = _key is not null;
            if (_key is not null) CryptographicOperations.ZeroMemory(_key);
            _key = null;
            _autoLock?.Dispose();
            _autoLock = null;
        }
        if (!wasUnlocked) return;
        StateChanged?.Invoke(this, EventArgs.Empty);
        _ = _log.LogAsync(ActivityActions.VaultLocked, reason, ActivityActor.System);
    }

    public void Touch()
    {
        lock (_gate)
        {
            if (_key is null) return;
            ScheduleAutoLock();
        }
    }

    public async Task<IReadOnlyList<VaultItem>> ListAsync(CancellationToken ct = default)
    {
        var key = RequireKey();
        Touch();
        var entries = await _repo.ListAsync(ct).ConfigureAwait(false);
        var items = new List<VaultItem>(entries.Count);
        foreach (var e in entries)
        {
            var p = Decrypt(key, e);
            items.Add(new VaultItem(e.Id, p.Title, p.Category, p.Username, p.Url, p.Notes, e.UpdatedAt));
        }
        return items.OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<Guid> SaveAsync(VaultItemEdit edit, CancellationToken ct = default)
    {
        var key = RequireKey();
        Touch();
        if (string.IsNullOrWhiteSpace(edit.Title)) throw new DomainException("Error.TitleRequired");

        if (edit.Id is null)
        {
            if (string.IsNullOrEmpty(edit.Password)) throw new DomainException("Vault.PasswordRequired");
            var entry = new VaultEntry { EncryptedPayload = Encrypt(key, ToPayload(edit, edit.Password)) };
            await _repo.AddAsync(entry, ct).ConfigureAwait(false);
            await _log.LogAsync(ActivityActions.VaultEntryAdded, entry.Id.ToString("N")[..8], ActivityActor.User, ct).ConfigureAwait(false);
            return entry.Id;
        }

        var existing = await _repo.GetAsync(edit.Id.Value, ct).ConfigureAwait(false) ?? throw new DomainException("Error.NotFound");
        var current = Decrypt(key, existing);
        var password = string.IsNullOrEmpty(edit.Password) ? current.Password : edit.Password;
        existing.EncryptedPayload = Encrypt(key, ToPayload(edit, password));
        existing.UpdatedAt = _clock.UtcNow;
        await _repo.UpdateAsync(existing, ct).ConfigureAwait(false);
        await _log.LogAsync(ActivityActions.VaultEntryEdited, existing.Id.ToString("N")[..8], ActivityActor.User, ct).ConfigureAwait(false);
        return existing.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        RequireKey();
        Touch();
        await _repo.DeleteAsync(id, ct).ConfigureAwait(false);
        await _log.LogAsync(ActivityActions.VaultEntryDeleted, id.ToString("N")[..8], ActivityActor.User, ct).ConfigureAwait(false);
    }

    public async Task<string> RevealPasswordAsync(Guid id, CancellationToken ct = default)
    {
        var key = RequireKey();
        Touch();
        var entry = await _repo.GetAsync(id, ct).ConfigureAwait(false) ?? throw new DomainException("Error.NotFound");
        var password = Decrypt(key, entry).Password;
        // The audit trail records THAT a password was revealed, never the password or the account name.
        await _log.LogAsync(ActivityActions.VaultPasswordRevealed, id.ToString("N")[..8], ActivityActor.User, ct).ConfigureAwait(false);
        return password;
    }

    public async Task ChangeMasterPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var oldKey = RequireKey();
        var meta = _meta ?? throw new DomainException("Vault.NotInitialized");
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < MinMasterPasswordLength) throw new DomainException("Vault.PasswordTooShort");

        var verifyKey = await Task.Run(() => _crypto.DeriveKey(currentPassword, meta.Salt, meta.Iterations), ct).ConfigureAwait(false);
        var ok = VerifyKey(verifyKey, meta);
        CryptographicOperations.ZeroMemory(verifyKey);
        if (!ok) throw new DomainException("Vault.WrongPassword");

        var salt = _crypto.GenerateSalt();
        var iterations = _crypto.DefaultIterations;
        var newKey = await Task.Run(() => _crypto.DeriveKey(newPassword, salt, iterations), ct).ConfigureAwait(false);

        var entries = await _repo.ListAsync(ct).ConfigureAwait(false);
        var reencrypted = new List<VaultEntry>(entries.Count);
        foreach (var e in entries)
        {
            var plain = _crypto.Decrypt(oldKey, e.EncryptedPayload);
            try { e.EncryptedPayload = _crypto.Encrypt(newKey, plain); }
            finally { CryptographicOperations.ZeroMemory(plain); }
            e.UpdatedAt = _clock.UtcNow;
            reencrypted.Add(e);
        }
        var newMeta = new VaultMeta { Salt = salt, Iterations = iterations, Verifier = _crypto.Encrypt(newKey, VerifierPlaintext) };
        await _repo.ReplaceAllAsync(newMeta, reencrypted, ct).ConfigureAwait(false);
        _meta = newMeta;
        SetUnlocked(newKey);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_key is not null) CryptographicOperations.ZeroMemory(_key);
            _key = null;
            _autoLock?.Dispose();
        }
    }

    // ---- helpers ----

    private bool VerifyKey(byte[] key, VaultMeta meta)
    {
        try
        {
            var plain = _crypto.Decrypt(key, meta.Verifier);
            return CryptographicOperations.FixedTimeEquals(plain, VerifierPlaintext);
        }
        catch (CryptographicException) { return false; }
    }

    private async Task<VaultUnlockResult> RegisterFailureAsync(VaultMeta meta, CancellationToken ct)
    {
        meta.FailedAttempts++;
        var lockout = AttemptPolicy.LockoutFor(meta.FailedAttempts);
        meta.LockedUntil = lockout > TimeSpan.Zero ? _clock.UtcNow + lockout : null;
        await _repo.SaveMetaAsync(meta, ct).ConfigureAwait(false);
        await _log.LogAsync(ActivityActions.VaultUnlockFailed, meta.FailedAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture), ActivityActor.System, ct).ConfigureAwait(false);
        return lockout > TimeSpan.Zero
            ? new VaultUnlockResult(VaultUnlockOutcome.LockedOut, lockout)
            : new VaultUnlockResult(VaultUnlockOutcome.WrongPassword, null, AttemptPolicy.RemainingFreeAttempts(meta.FailedAttempts));
    }

    private void SetUnlocked(byte[] key)
    {
        lock (_gate)
        {
            if (_key is not null) CryptographicOperations.ZeroMemory(_key);
            _key = key;
            ScheduleAutoLock();
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ScheduleAutoLock()
    {
        var minutes = Math.Max(1, _settings.Current.VaultAutoLockMinutes);
        _autoLock?.Dispose();
        _autoLock = new Timer(_ => Lock("auto-lock"), null, TimeSpan.FromMinutes(minutes), Timeout.InfiniteTimeSpan);
    }

    private byte[] RequireKey()
    {
        lock (_gate) return _key ?? throw new DomainException("Vault.Locked");
    }

    private sealed class Payload
    {
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? Url { get; set; }
        public string? Notes { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    private static Payload ToPayload(VaultItemEdit e, string password) => new()
    {
        Title = e.Title.Trim(), Category = e.Category?.Trim() ?? string.Empty, Username = e.Username?.Trim(),
        Url = e.Url?.Trim(), Notes = e.Notes, Password = password
    };

    private byte[] Encrypt(byte[] key, Payload payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        try { return _crypto.Encrypt(key, bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private Payload Decrypt(byte[] key, VaultEntry entry)
    {
        var bytes = _crypto.Decrypt(key, entry.EncryptedPayload);
        try { return JsonSerializer.Deserialize<Payload>(bytes) ?? throw new CryptographicException("Invalid payload."); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
