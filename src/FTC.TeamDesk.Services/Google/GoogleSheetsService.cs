using System.Text.Json;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Services.Google;

public sealed class GoogleSheetsService : IGoogleSheetsService
{
    private const string KeySheetId = "google.spreadsheetId";
    private const string KeySheetTitle = "google.spreadsheetTitle";
    private const string KeyTab = "google.tab";
    private const string KeyLastSync = "google.lastSync";
    private const string KeyMapping = "google.mapping";

    private readonly GoogleAuthService _auth;
    private readonly GoogleSheetsClient _client;
    private readonly ISettingsService _settings;
    private readonly IApplicationService _applications;
    private readonly IActivityLogService _log;
    private readonly IAppLogger _logger;
    private string? _lastErrorKey;

    public GoogleSheetsService(GoogleAuthService auth, GoogleSheetsClient client, ISettingsService settings,
        IApplicationService applications, IActivityLogService log, IAppLogger logger)
    {
        _auth = auth; _client = client; _settings = settings; _applications = applications; _log = log; _logger = logger;
    }

    public async Task<GoogleConnectionStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var connected = await _auth.HasTokensAsync(ct).ConfigureAwait(false);
        var sheetId = await _settings.GetValueAsync(KeySheetId, ct).ConfigureAwait(false);
        var title = await _settings.GetValueAsync(KeySheetTitle, ct).ConfigureAwait(false);
        var tab = await _settings.GetValueAsync(KeyTab, ct).ConfigureAwait(false);
        var lastSync = await _settings.GetValueAsync(KeyLastSync, ct).ConfigureAwait(false);
        DateTime? last = DateTime.TryParse(lastSync, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d : null;

        var state = !connected ? ConnectionState.Disconnected : _lastErrorKey is null ? ConnectionState.Connected : ConnectionState.Error;
        var email = connected ? await _auth.GetAccountEmailAsync(ct).ConfigureAwait(false) : null;
        return new GoogleConnectionStatus(state, email, sheetId, title, tab, _lastErrorKey, last);
    }

    public async Task<OperationResult> ConnectAsync(string clientId, string? clientSecret, CancellationToken ct = default)
    {
        try
        {
            var prefs = _settings.Current;
            prefs.GoogleClientId = clientId.Trim();
            await _settings.SaveAsync(prefs, ct).ConfigureAwait(false);
            await _auth.ConnectAsync(clientId, clientSecret, ct).ConfigureAwait(false);
            _lastErrorKey = null;
            await _log.LogAsync(ActivityActions.GoogleConnected, string.Empty, ActivityActor.User, ct).ConfigureAwait(false);
            return OperationResult.Ok();
        }
        catch (GoogleAuthException ex) { _lastErrorKey = ex.ErrorKey; return OperationResult.Fail(ex.ErrorKey, ex.Message); }
        catch (OperationCanceledException) { return OperationResult.Fail("Error.Cancelled"); }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _auth.DisconnectAsync(ct).ConfigureAwait(false);
        _lastErrorKey = null;
        await _log.LogAsync(ActivityActions.GoogleDisconnected, string.Empty, ActivityActor.User, ct).ConfigureAwait(false);
    }

    public Task<OperationResult<IReadOnlyList<SpreadsheetInfo>>> ListSpreadsheetsAsync(CancellationToken ct = default)
        => GuardAsync<IReadOnlyList<SpreadsheetInfo>>(() => _client.ListSpreadsheetsAsync(ct));

    public Task<OperationResult<IReadOnlyList<string>>> ListTabsAsync(string spreadsheetIdOrUrl, CancellationToken ct = default)
        => GuardAsync<IReadOnlyList<string>>(async () =>
        {
            var (_, tabs) = await _client.GetSpreadsheetAsync(GoogleSheetsClient.ExtractSpreadsheetId(spreadsheetIdOrUrl), ct).ConfigureAwait(false);
            return tabs;
        });

    public async Task<OperationResult> SelectSheetAsync(string spreadsheetIdOrUrl, string tab, CancellationToken ct = default)
    {
        var result = await GuardAsync<string>(async () =>
        {
            var id = GoogleSheetsClient.ExtractSpreadsheetId(spreadsheetIdOrUrl);
            var (title, tabs) = await _client.GetSpreadsheetAsync(id, ct).ConfigureAwait(false);
            if (!tabs.Contains(tab)) throw new GoogleApiException("Google.Error.TabNotFound");
            await _settings.SetValueAsync(KeySheetId, id, ct).ConfigureAwait(false);
            await _settings.SetValueAsync(KeySheetTitle, title, ct).ConfigureAwait(false);
            await _settings.SetValueAsync(KeyTab, tab, ct).ConfigureAwait(false);
            return title;
        }).ConfigureAwait(false);
        return result.Succeeded ? OperationResult.Ok() : OperationResult.Fail(result.ErrorKey!, result.Detail);
    }

    public async Task<OperationResult> TestConnectionAsync(CancellationToken ct = default)
    {
        var sheetId = await _settings.GetValueAsync(KeySheetId, ct).ConfigureAwait(false);
        var result = await GuardAsync<int>(async () =>
        {
            if (string.IsNullOrEmpty(sheetId))
            {
                await _client.ListSpreadsheetsAsync(ct).ConfigureAwait(false); // proves token + Drive scope work
                return 0;
            }
            await _client.GetSpreadsheetAsync(sheetId, ct).ConfigureAwait(false);
            return 1;
        }).ConfigureAwait(false);
        return result.Succeeded ? OperationResult.Ok() : OperationResult.Fail(result.ErrorKey!, result.Detail);
    }

    public async Task<OperationResult<SheetSyncResult>> SyncApplicationsAsync(CancellationToken ct = default)
    {
        var sheetId = await _settings.GetValueAsync(KeySheetId, ct).ConfigureAwait(false);
        var tab = await _settings.GetValueAsync(KeyTab, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(sheetId) || string.IsNullOrEmpty(tab)) return OperationResult<SheetSyncResult>.Fail("Google.Error.NoSheetSelected");

        var mappingJson = await _settings.GetValueAsync(KeyMapping, ct).ConfigureAwait(false);
        ColumnMapping? mapping = null;
        if (!string.IsNullOrEmpty(mappingJson))
        {
            try { mapping = JsonSerializer.Deserialize<ColumnMapping>(mappingJson); } catch (JsonException) { /* ignore bad mapping */ }
        }

        return await GuardAsync(async () =>
        {
            var rows = await _client.GetRowsAsync(sheetId, tab, ct).ConfigureAwait(false);
            var apps = ApplicationRowMapper.Map(sheetId, tab, rows, mapping);
            var added = await _applications.ImportAsync(apps, ct).ConfigureAwait(false);
            await _settings.SetValueAsync(KeyLastSync, DateTime.UtcNow.ToString("o"), ct).ConfigureAwait(false);
            return new SheetSyncResult(Math.Max(0, rows.Count - 1), added, apps.Count - added);
        }).ConfigureAwait(false);
    }

    private async Task<OperationResult<T>> GuardAsync<T>(Func<Task<T>> action)
    {
        try
        {
            var value = await action().ConfigureAwait(false);
            _lastErrorKey = null;
            return OperationResult<T>.Ok(value);
        }
        catch (GoogleApiException ex) { _lastErrorKey = ex.ErrorKey; _logger.Warning($"Google API error: {ex.ErrorKey} {ex.Message}"); return OperationResult<T>.Fail(ex.ErrorKey, ex.Message); }
        catch (GoogleAuthException ex) { _lastErrorKey = ex.ErrorKey; _logger.Warning($"Google auth error: {ex.ErrorKey}"); return OperationResult<T>.Fail(ex.ErrorKey, ex.Message); }
        catch (OperationCanceledException) { return OperationResult<T>.Fail("Error.Cancelled"); }
    }
}
