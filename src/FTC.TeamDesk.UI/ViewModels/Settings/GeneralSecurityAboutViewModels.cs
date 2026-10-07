using System.Collections.ObjectModel;
using System.Reflection;
using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.UI.Abstractions;

namespace FTC.TeamDesk.UI.ViewModels.Settings;

public sealed class GeneralSettingsViewModel : SettingsSection
{
    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private readonly IActivityLogService _log;
    private SelectOption<string>? _language;
    private SelectOption<ThemeMode>? _themeMode;
    private SelectOption<StartupPage>? _startup;
    private bool _notifications, _loading;

    public GeneralSettingsViewModel(UiContext ui, ISettingsService settings, IThemeService theme, IActivityLogService log) : base(ui)
    { _settings = settings; _theme = theme; _log = log; }

    public ObservableCollection<SelectOption<string>> Languages { get; } = new();
    public ObservableCollection<SelectOption<ThemeMode>> Themes { get; } = new();
    public ObservableCollection<SelectOption<StartupPage>> Startups { get; } = new();

    public SelectOption<string>? Language { get => _language; set { if (SetProperty(ref _language, value) && !_loading && value is not null) _ = ApplyLanguageAsync(value.Value); } }
    public SelectOption<ThemeMode>? Theme { get => _themeMode; set { if (SetProperty(ref _themeMode, value) && !_loading && value is not null) _ = ApplyAsync(p => p.Theme = value.Value, () => _theme.Apply(value.Value)); } }
    public SelectOption<StartupPage>? Startup { get => _startup; set { if (SetProperty(ref _startup, value) && !_loading && value is not null) _ = ApplyAsync(p => p.Startup = value.Value); } }
    public bool Notifications { get => _notifications; set { if (SetProperty(ref _notifications, value) && !_loading) _ = ApplyAsync(p => p.NotificationsEnabled = value); } }

    public override Task LoadAsync(CancellationToken ct)
    {
        _loading = true;
        var p = _settings.Current;
        Languages.Clear(); Themes.Clear(); Startups.Clear();
        foreach (var l in L.SupportedLanguages) Languages.Add(new(l.Code, l.NativeName));
        foreach (var t in Enum.GetValues<ThemeMode>()) Themes.Add(new(t, Ui.Format.EnumText(t)));
        foreach (var s in Enum.GetValues<StartupPage>()) Startups.Add(new(s, Ui.Format.EnumText(s)));
        Language = Languages.FirstOrDefault(l => l.Value == L.CurrentLanguage);
        Theme = Themes.First(t => t.Value == p.Theme);
        Startup = Startups.First(s => s.Value == p.Startup);
        Notifications = p.NotificationsEnabled;
        _loading = false;
        return Task.CompletedTask;
    }

    private async Task ApplyLanguageAsync(string code)
    {
        try
        {
            var p = _settings.Current;
            p.Language = code;
            await _settings.SaveAsync(p);
            L.SetLanguage(code); // raises LanguageChanged: the shell reloads the page with new texts instantly
        }
        catch (Exception ex) { Ui.Errors.Show(ex); }
    }

    private async Task ApplyAsync(Action<Core.Models.UserPreferences> change, Action? after = null)
    {
        try
        {
            var p = _settings.Current;
            change(p);
            await _settings.SaveAsync(p);
            after?.Invoke();
        }
        catch (Exception ex) { Ui.Errors.Show(ex); }
    }
}

public sealed class SecuritySettingsViewModel : SettingsSection
{
    private readonly ISettingsService _settings;
    private int _autoLock, _clipboard;
    private bool _onMinimize, _onSession, _loading;

    public SecuritySettingsViewModel(UiContext ui, ISettingsService settings) : base(ui) { _settings = settings; }

    public IReadOnlyList<int> AutoLockChoices { get; } = new[] { 1, 2, 5, 10, 15, 30 };
    public IReadOnlyList<int> ClipboardChoices { get; } = new[] { 10, 20, 30, 60 };
    public int AutoLockMinutes { get => _autoLock; set { if (SetProperty(ref _autoLock, value)) Save(p => p.VaultAutoLockMinutes = value); } }
    public int ClipboardSeconds { get => _clipboard; set { if (SetProperty(ref _clipboard, value)) Save(p => p.ClipboardClearSeconds = value); } }
    public bool LockOnMinimize { get => _onMinimize; set { if (SetProperty(ref _onMinimize, value)) Save(p => p.LockVaultOnMinimize = value); } }
    public bool LockOnSession { get => _onSession; set { if (SetProperty(ref _onSession, value)) Save(p => p.LockVaultOnSessionLock = value); } }

    public override Task LoadAsync(CancellationToken ct)
    {
        _loading = true;
        var p = _settings.Current;
        AutoLockMinutes = p.VaultAutoLockMinutes; ClipboardSeconds = p.ClipboardClearSeconds;
        LockOnMinimize = p.LockVaultOnMinimize; LockOnSession = p.LockVaultOnSessionLock;
        _loading = false;
        return Task.CompletedTask;
    }

    private void Save(Action<Core.Models.UserPreferences> change)
    {
        if (_loading) return;
        _ = Task.Run(async () =>
        {
            try { var p = _settings.Current; change(p); await _settings.SaveAsync(p); }
            catch (Exception ex) { Ui.Errors.Show(ex); }
        });
    }
}

public sealed class AboutViewModel : SettingsSection
{
    private readonly IDatabaseMaintenance _db;
    public AboutViewModel(UiContext ui, IDatabaseMaintenance db) : base(ui) { _db = db; }

    public string Version { get; } = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
    public string Runtime { get; } = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
    public string DatabasePath => _db.DatabasePath;
    public override Task LoadAsync(CancellationToken ct) { Raise(nameof(DatabasePath)); return Task.CompletedTask; }
}
