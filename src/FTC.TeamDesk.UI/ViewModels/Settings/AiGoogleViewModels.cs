using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.AI.Providers;
using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Settings;

public sealed class PermissionRow : ObservableObject
{
    private bool _enabled;
    private readonly Func<PermissionRow, bool, Task> _save;
    private bool _loading = true;

    public PermissionRow(AiPermissionKey key, string label, string description, bool enabled, bool isWrite, bool isSensitive, Func<PermissionRow, bool, Task> save)
    { Key = key; Label = label; Description = description; _enabled = enabled; IsWrite = isWrite; IsSensitive = isSensitive; _save = save; _loading = false; }

    public AiPermissionKey Key { get; }
    public string Label { get; }
    public string Description { get; }
    public bool IsWrite { get; }
    public bool IsSensitive { get; }
    public bool IsEnabled
    {
        get => _enabled;
        set { if (SetProperty(ref _enabled, value) && !_loading) _ = _save(this, value); }
    }
    public void Reset(bool value) { _loading = true; IsEnabled = value; _loading = false; }
}

public sealed class AiSettingsViewModel : SettingsSection
{
    private readonly IAiConfigRepository _config;
    private readonly ISecretStore _secrets;
    private readonly IModelCatalog _models;
    private readonly IActivityLogService _log;
    private SelectOption<AiProviderKind>? _provider;
    private string _model = "", _baseUrl = "", _apiKey = "", _status = "";
    private double _temperature = 0.2;
    private int _maxTokens = 1024;
    private bool _enabled, _hasKey, _loading, _busy;
    private bool _statusIsError;

    public AiSettingsViewModel(UiContext ui, IAiConfigRepository config, ISecretStore secrets, IModelCatalog models, IActivityLogService log) : base(ui)
    {
        _config = config; _secrets = secrets; _models = models; _log = log;
        foreach (var p in AiProviderInfo.All) Providers.Add(new(p.Kind, p.DisplayName));
        SaveCommand = Cmd(SaveAsync);
        LoadModelsCommand = Cmd(LoadModelsAsync, () => !_busy);
        ClearKeyCommand = Cmd(ClearKeyAsync, () => _hasKey);
    }

    public ObservableCollection<SelectOption<AiProviderKind>> Providers { get; } = new();
    public ObservableCollection<string> ModelChoices { get; } = new();
    public ObservableCollection<PermissionRow> ReadPermissions { get; } = new();
    public ObservableCollection<PermissionRow> WritePermissions { get; } = new();
    public PermissionRow? VaultPermission { get; private set; }

    public SelectOption<AiProviderKind>? Provider
    {
        get => _provider;
        set
        {
            if (!SetProperty(ref _provider, value) || value is null) return;
            Raise(nameof(NeedsApiKey), nameof(UsesBaseUrl), nameof(BaseUrlPlaceholder));
            if (!_loading) { ModelChoices.Clear(); Model = ""; _ = RefreshKeyStateAsync(); }
        }
    }
    public string Model { get => _model; set => SetProperty(ref _model, value); }
    public string BaseUrl { get => _baseUrl; set => SetProperty(ref _baseUrl, value); }
    /// <summary>Entered key. Never displayed after saving; the field is cleared and only "key saved" is shown.</summary>
    public string ApiKey { get => _apiKey; set => SetProperty(ref _apiKey, value); }
    public double Temperature { get => _temperature; set => SetProperty(ref _temperature, Math.Round(Math.Clamp(value, 0, 1), 1)); }
    public int MaxTokens { get => _maxTokens; set => SetProperty(ref _maxTokens, Math.Clamp(value, 64, 32000)); }
    public bool IsEnabled { get => _enabled; set => SetProperty(ref _enabled, value); }
    public bool HasKey { get => _hasKey; private set { if (SetProperty(ref _hasKey, value)) (ClearKeyCommand as IRaiseCanExecute)?.RaiseCanExecuteChanged(); } }
    public string Status { get => _status; private set { if (SetProperty(ref _status, value)) Raise(nameof(HasStatus)); } }
    public bool StatusIsError { get => _statusIsError; private set => SetProperty(ref _statusIsError, value); }
    public bool HasStatus => !string.IsNullOrEmpty(_status);
    public bool IsBusy { get => _busy; private set { if (SetProperty(ref _busy, value)) (LoadModelsCommand as IRaiseCanExecute)?.RaiseCanExecuteChanged(); } }

    public bool NeedsApiKey => _provider is not null && AiProviderInfo.For(_provider.Value).RequiresApiKey;
    public bool UsesBaseUrl => _provider is not null && AiProviderInfo.For(_provider.Value).UsesBaseUrl;
    public string BaseUrlPlaceholder => _provider is null ? "" : AiProviderInfo.For(_provider.Value).DefaultBaseUrl ?? "https://host/v1";

    public ICommand SaveCommand { get; }
    public ICommand LoadModelsCommand { get; }
    public ICommand ClearKeyCommand { get; }

    public override async Task LoadAsync(CancellationToken ct)
    {
        _loading = true;
        var s = await _config.GetSettingsAsync(ct);
        Provider = Providers.First(p => p.Value == s.Provider);
        Model = s.Model; BaseUrl = s.BaseUrl ?? ""; Temperature = s.Temperature; MaxTokens = s.MaxTokens; IsEnabled = s.IsEnabled;
        if (!string.IsNullOrEmpty(s.Model)) ModelChoices.Add(s.Model);
        await RefreshKeyStateAsync();

        var perms = await _config.GetPermissionsAsync(ct);
        ReadPermissions.Clear(); WritePermissions.Clear();
        foreach (var key in Enum.GetValues<AiPermissionKey>())
        {
            var isWrite = key is AiPermissionKey.AddPurchase or AiPermissionKey.EditPurchase or AiPermissionKey.DeletePurchase
                or AiPermissionKey.EditMember or AiPermissionKey.ChangeApplicationStatus;
            var row = new PermissionRow(key, L["AiPermission." + key], L["AiPermission." + key + ".Hint"], perms.GetValueOrDefault(key), isWrite,
                key == AiPermissionKey.AccessPasswordVault, SavePermissionAsync);
            if (key == AiPermissionKey.AccessPasswordVault) VaultPermission = row;
            else (isWrite ? WritePermissions : ReadPermissions).Add(row);
        }
        Raise(nameof(VaultPermission));
        _loading = false;
    }

    private async Task RefreshKeyStateAsync()
    {
        HasKey = _provider is not null && await _secrets.ExistsAsync(SecretNames.AiKeyFor(_provider.Value));
    }

    private async Task SavePermissionAsync(PermissionRow row, bool value)
    {
        try
        {
            await _config.SetPermissionAsync(row.Key, value);
            await _log.LogAsync(ActivityActions.SettingsChanged, $"AI permission {row.Key}: {(value ? "on" : "off")}");
        }
        catch (Exception ex) { Ui.Errors.Show(ex); row.Reset(!value); }
    }

    private async Task SaveAsync()
    {
        if (_provider is null) return;
        var settings = new AiSettings
        {
            Provider = _provider.Value, Model = _model.Trim(), Temperature = _temperature, MaxTokens = _maxTokens,
            BaseUrl = string.IsNullOrWhiteSpace(_baseUrl) ? null : _baseUrl.Trim(), IsEnabled = _enabled
        };
        await _config.SaveSettingsAsync(settings);
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            await _secrets.SetAsync(SecretNames.AiKeyFor(_provider.Value), _apiKey.Trim());
            ApiKey = "";
            HasKey = true;
        }
        await _log.LogAsync(ActivityActions.SettingsChanged, "AI settings");
        SetStatus(L["Settings.Ai.Saved"], false);
        Ui.Toasts.Show(L["Settings.Ai.Saved"], ToastKind.Success);
    }

    private async Task ClearKeyAsync()
    {
        if (_provider is null) return;
        if (!await Ui.Dialogs.ConfirmAsync(L["Settings.Ai.ClearKey.Title"], L["Settings.Ai.ClearKey.Message"], L["Common.Delete"], true)) return;
        await _secrets.DeleteAsync(SecretNames.AiKeyFor(_provider.Value));
        HasKey = false;
    }

    /// <summary>Fetches the model list dynamically from the provider; the key typed in the field (if any) is used without saving it.</summary>
    private async Task LoadModelsAsync()
    {
        if (_provider is null) return;
        IsBusy = true; SetStatus(L["Settings.Ai.LoadingModels"], false);
        try
        {
            var key = !string.IsNullOrWhiteSpace(_apiKey) ? _apiKey.Trim() : await _secrets.GetAsync(SecretNames.AiKeyFor(_provider.Value));
            var r = await _models.ListModelsAsync(_provider.Value, string.IsNullOrWhiteSpace(_baseUrl) ? null : _baseUrl.Trim(), key, CancellationToken.None);
            if (!r.Succeeded) { SetStatus(L[r.ErrorKey ?? "Error.Unexpected"], true); return; }
            ModelChoices.Clear();
            foreach (var m in r.Value!) ModelChoices.Add(m);
            if (string.IsNullOrEmpty(_model) && ModelChoices.Count > 0) Model = ModelChoices[0];
            SetStatus(L.Format("Settings.Ai.ModelsLoaded", ModelChoices.Count), false);
        }
        finally { IsBusy = false; }
    }

    private void SetStatus(string text, bool error) { Status = text; StatusIsError = error; }
}

public sealed class SheetChoice
{
    public SheetChoice(SpreadsheetInfo info) { Info = info; }
    public SpreadsheetInfo Info { get; }
    public override string ToString() => Info.Name;
}

public sealed class GoogleSettingsViewModel : SettingsSection
{
    private readonly IGoogleSheetsService _google;
    private readonly ISettingsService _settings;
    private readonly ISecretStore _secrets;
    private GoogleConnectionStatus? _status;
    private string _clientId = "", _clientSecret = "", _message = "";
    private SheetChoice? _sheet;
    private string? _tab;
    private bool _busy, _messageError;

    public GoogleSettingsViewModel(UiContext ui, IGoogleSheetsService google, ISettingsService settings, ISecretStore secrets) : base(ui)
    {
        _google = google; _settings = settings; _secrets = secrets;
        ConnectCommand = Cmd(ConnectAsync, () => !_busy);
        DisconnectCommand = Cmd(DisconnectAsync, () => !_busy);
        ListSheetsCommand = Cmd(ListSheetsAsync, () => !_busy);
        SelectCommand = Cmd(SelectAsync, () => !_busy && _sheet is not null && _tab is not null);
        TestCommand = Cmd(TestAsync, () => !_busy);
    }

    public ObservableCollection<SheetChoice> Sheets { get; } = new();
    public ObservableCollection<string> Tabs { get; } = new();

    public string ClientId { get => _clientId; set => SetProperty(ref _clientId, value); }
    public string ClientSecret { get => _clientSecret; set => SetProperty(ref _clientSecret, value); }
    public SheetChoice? Sheet { get => _sheet; set { if (SetProperty(ref _sheet, value)) { _ = LoadTabsAsync(); Refresh(); } } }
    public string? Tab { get => _tab; set { if (SetProperty(ref _tab, value)) Refresh(); } }
    public bool IsBusy { get => _busy; private set { if (SetProperty(ref _busy, value)) Refresh(); } }
    public string Message { get => _message; private set { if (SetProperty(ref _message, value)) Raise(nameof(HasMessage)); } }
    public bool HasMessage => !string.IsNullOrEmpty(_message);
    public bool MessageIsError { get => _messageError; private set => SetProperty(ref _messageError, value); }

    public bool IsConnected => _status?.State == ConnectionState.Connected;
    public bool IsDisconnected => !IsConnected;
    public string StateText => L["ConnectionState." + (_status?.State ?? ConnectionState.Disconnected)];
    public StatusTone StateTone => _status?.State switch { ConnectionState.Connected => StatusTone.Success, ConnectionState.Error => StatusTone.Danger, _ => StatusTone.Neutral };
    public string Account => _status?.AccountEmail ?? "-";
    public string CurrentSheet => _status?.SpreadsheetTitle is { } t ? $"{t} / {_status.SheetTab}" : L["Common.None"];
    public string LastSync => _status?.LastSyncUtc is { } d ? Ui.Format.DateTimeText(d.ToLocalTime()) : "-";

    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand ListSheetsCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand TestCommand { get; }

    private void Refresh()
    {
        foreach (var c in new[] { ConnectCommand, DisconnectCommand, ListSheetsCommand, SelectCommand, TestCommand }) (c as IRaiseCanExecute)?.RaiseCanExecuteChanged();
    }

    public override async Task LoadAsync(CancellationToken ct)
    {
        ClientId = _settings.Current.GoogleClientId ?? "";
        await ReloadStatusAsync();
    }

    private async Task ReloadStatusAsync()
    {
        _status = await _google.GetStatusAsync();
        Raise(nameof(IsConnected), nameof(IsDisconnected), nameof(StateText), nameof(StateTone), nameof(Account), nameof(CurrentSheet), nameof(LastSync));
    }

    private void Report(OperationResult r, string okKey)
    {
        Message = r.Succeeded ? L[okKey] : L[r.ErrorKey ?? "Error.Unexpected"];
        MessageIsError = !r.Succeeded;
    }

    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(_clientId)) { Message = L["Google.Error.ClientIdRequired"]; MessageIsError = true; return; }
        IsBusy = true; Message = L["Settings.Google.WaitingBrowser"]; MessageIsError = false;
        try
        {
            var r = await _google.ConnectAsync(_clientId, string.IsNullOrWhiteSpace(_clientSecret) ? null : _clientSecret.Trim());
            ClientSecret = "";
            Report(r, "Settings.Google.Connected");
            await ReloadStatusAsync();
        }
        finally { IsBusy = false; }
    }

    private async Task DisconnectAsync()
    {
        if (!await Ui.Dialogs.ConfirmAsync(L["Settings.Google.Disconnect.Title"], L["Settings.Google.Disconnect.Message"], L["Settings.Google.Disconnect"], true)) return;
        await _google.DisconnectAsync();
        Sheets.Clear(); Tabs.Clear();
        Message = L["Settings.Google.Disconnected"]; MessageIsError = false;
        await ReloadStatusAsync();
    }

    private async Task ListSheetsAsync()
    {
        IsBusy = true;
        try
        {
            var r = await _google.ListSpreadsheetsAsync();
            if (!r.Succeeded) { Message = L[r.ErrorKey ?? "Error.Unexpected"]; MessageIsError = true; return; }
            Sheets.Clear();
            foreach (var s in r.Value!) Sheets.Add(new SheetChoice(s));
            Message = L.Format("Settings.Google.SheetsFound", Sheets.Count); MessageIsError = false;
        }
        finally { IsBusy = false; }
    }

    private async Task LoadTabsAsync()
    {
        Tabs.Clear(); Tab = null;
        if (_sheet is null) return;
        var r = await _google.ListTabsAsync(_sheet.Info.Id);
        if (!r.Succeeded) { Message = L[r.ErrorKey ?? "Error.Unexpected"]; MessageIsError = true; return; }
        foreach (var t in r.Value!) Tabs.Add(t);
        Tab = Tabs.FirstOrDefault();
    }

    private async Task SelectAsync()
    {
        if (_sheet is null || _tab is null) return;
        IsBusy = true;
        try
        {
            var r = await _google.SelectSheetAsync(_sheet.Info.Id, _tab);
            Report(r, "Settings.Google.SheetSelected");
            await ReloadStatusAsync();
        }
        finally { IsBusy = false; }
    }

    private async Task TestAsync()
    {
        IsBusy = true; Message = L["Settings.Google.Testing"]; MessageIsError = false;
        try { Report(await _google.TestConnectionAsync(), "Settings.Google.TestOk"); await ReloadStatusAsync(); }
        finally { IsBusy = false; }
    }
}
