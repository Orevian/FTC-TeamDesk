using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Tests.Fakes;

public sealed class FakeVaultRepository : IVaultRepository
{
    public VaultMeta? Meta;
    public readonly List<VaultEntry> Entries = new();
    public Task<VaultMeta?> GetMetaAsync(CancellationToken ct = default) => Task.FromResult(Meta is null ? null : Clone(Meta));
    public Task SaveMetaAsync(VaultMeta meta, CancellationToken ct = default) { Meta = Clone(meta); return Task.CompletedTask; }
    public Task<IReadOnlyList<VaultEntry>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<VaultEntry>>(Entries.Select(Clone).ToList());
    public Task<VaultEntry?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Entries.Where(e => e.Id == id).Select(Clone).FirstOrDefault());
    public Task AddAsync(VaultEntry entry, CancellationToken ct = default) { Entries.Add(Clone(entry)); return Task.CompletedTask; }
    public Task UpdateAsync(VaultEntry entry, CancellationToken ct = default) { Entries[Entries.FindIndex(e => e.Id == entry.Id)] = Clone(entry); return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { Entries.RemoveAll(e => e.Id == id); return Task.CompletedTask; }
    public Task ReplaceAllAsync(VaultMeta meta, IReadOnlyList<VaultEntry> entries, CancellationToken ct = default)
    { Meta = Clone(meta); foreach (var e in entries) Entries[Entries.FindIndex(x => x.Id == e.Id)] = Clone(e); return Task.CompletedTask; }

    private static VaultMeta Clone(VaultMeta m) => new() { Id = m.Id, Salt = m.Salt.ToArray(), Iterations = m.Iterations, Verifier = m.Verifier.ToArray(), FailedAttempts = m.FailedAttempts, LockedUntil = m.LockedUntil };
    private static VaultEntry Clone(VaultEntry e) => new() { Id = e.Id, EncryptedPayload = e.EncryptedPayload.ToArray(), CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt };
}

public sealed class FakeClock : IClock
{
    public DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
    public DateTime UtcNow => Now;
    public DateTime Today => Now.Date;
    public void Advance(TimeSpan span) => Now += span;
}

public sealed class RecordingLog : IActivityLogService
{
    public readonly List<(string Type, string Description)> Entries = new();
    public Task LogAsync(string actionType, string description, ActivityActor actor = ActivityActor.User, CancellationToken ct = default)
    { lock (Entries) Entries.Add((actionType, description)); return Task.CompletedTask; }
    public Task<IReadOnlyList<ActivityLogEntry>> RecentAsync(int count, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ActivityLogEntry>>(Array.Empty<ActivityLogEntry>());
    public Task<PagedResult<ActivityLogEntry>> QueryAsync(ActivityQuery query, CancellationToken ct = default) => Task.FromResult(new PagedResult<ActivityLogEntry>(Array.Empty<ActivityLogEntry>(), 0, 1, 50));
}

public sealed class FakeSettings : ISettingsService
{
    public UserPreferences Current { get; set; } = new();
    public event EventHandler? PreferencesChanged;
    public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveAsync(UserPreferences preferences, CancellationToken ct = default) { Current = preferences; PreferencesChanged?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; }
    public Task<string?> GetValueAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>(null);
    public Task SetValueAsync(string key, string? value, CancellationToken ct = default) => Task.CompletedTask;
}
