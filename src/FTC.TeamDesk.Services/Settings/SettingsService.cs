using System.Text.Json;
using System.Text.Json.Serialization;
using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Services;

/// <summary>
/// Machine-local preferences live in a JSON file (loaded before the database exists so language/theme apply at start-up);
/// data-scoped key/values (Google sheet selection etc.) live in the AppSettings table and travel with backups.
/// No secrets are stored in either.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _configPath;
    private readonly IAppSettingsRepository? _repo;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SettingsService(string configPath, IAppSettingsRepository? repo, IAppLogger logger)
    {
        _configPath = configPath; _repo = repo; _logger = logger;
        Current = ReadFile(configPath, logger);
    }

    public UserPreferences Current { get; private set; }
    public event EventHandler? PreferencesChanged;

    /// <summary>Synchronous read used at process start-up, before DI is built.</summary>
    public static UserPreferences ReadFile(string path, IAppLogger? logger = null)
    {
        try
        {
            if (!File.Exists(path)) return new UserPreferences();
            return JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(path), Json) ?? new UserPreferences();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger?.Error("Could not read preferences; using defaults.", ex);
            return new UserPreferences();
        }
    }

    public Task LoadAsync(CancellationToken ct = default)
    {
        Current = ReadFile(_configPath, _logger);
        return Task.CompletedTask;
    }

    public async Task SaveAsync(UserPreferences preferences, CancellationToken ct = default)
    {
        Validate(preferences);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var temp = _configPath + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(preferences, Json), ct).ConfigureAwait(false);
            File.Move(temp, _configPath, overwrite: true); // atomic replace: never leaves a half-written file
            Current = preferences;
        }
        finally { _gate.Release(); }
        PreferencesChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<string?> GetValueAsync(string key, CancellationToken ct = default)
        => _repo is null ? Task.FromResult<string?>(null) : _repo.GetAsync(key, ct);

    public Task SetValueAsync(string key, string? value, CancellationToken ct = default)
        => _repo is null ? Task.CompletedTask : _repo.SetAsync(key, value, ct);

    private static void Validate(UserPreferences p)
    {
        p.VaultAutoLockMinutes = Math.Clamp(p.VaultAutoLockMinutes, 1, 240);
        p.ClipboardClearSeconds = Math.Clamp(p.ClipboardClearSeconds, 5, 300);
        if (p.Language is not ("auto" or "tr" or "en")) p.Language = "auto";
    }
}
