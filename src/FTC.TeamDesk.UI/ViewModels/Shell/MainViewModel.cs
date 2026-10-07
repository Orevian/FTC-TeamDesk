using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;
using FTC.TeamDesk.UI.ViewModels.Analytics;
using FTC.TeamDesk.UI.ViewModels.Applications;
using FTC.TeamDesk.UI.ViewModels.Assistant;
using FTC.TeamDesk.UI.ViewModels.Budget;
using FTC.TeamDesk.UI.ViewModels.Dashboard;
using FTC.TeamDesk.UI.ViewModels.Settings;
using FTC.TeamDesk.UI.ViewModels.Team;
using FTC.TeamDesk.UI.ViewModels.Vault;

namespace FTC.TeamDesk.UI.ViewModels.Shell;

/// <summary>Window-level state: sidebar navigation, current page, dialog and toast hosting, global shortcuts.</summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly INavigationService _nav;
    private readonly IVaultService _vault;
    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private readonly Dictionary<string, Func<Task>> _routes;
    private string _activeSection = "dashboard";

    public MainViewModel(INavigationService nav, DialogService dialogs, ToastService toasts, UiContext ui, IVaultService vault,
        ISettingsService settings, IThemeService theme)
    {
        _nav = nav; _vault = vault; _settings = settings; _theme = theme;
        Dialogs = dialogs; Toasts = toasts; L = ui.Loc;

        NavItems = new[]
        {
            new NavItem("dashboard", "Nav.Dashboard", ""),
            new NavItem("team", "Nav.Team", ""),
            new NavItem("applications", "Nav.Applications", ""),
            new NavItem("budget", "Nav.Budget", ""),
            new NavItem("analytics", "Nav.Analytics", ""),
            new NavItem("vault", "Nav.Vault", ""),
            new NavItem("assistant", "Nav.Assistant", ""),
            new NavItem("settings", "Nav.Settings", "")
        };

        Nav = new ObservableCollection<NavEntry>(NavItems.Select((n, i) => new NavEntry(n, i + 1)));
        RefreshNav();

        _routes = new()
        {
            ["dashboard"] = () => nav.NavigateAsync<DashboardViewModel>(),
            ["team"] = () => nav.NavigateAsync<TeamViewModel>(),
            ["applications"] = () => nav.NavigateAsync<ApplicationsViewModel>(),
            ["budget"] = () => nav.NavigateAsync<BudgetViewModel>(),
            ["analytics"] = () => nav.NavigateAsync<AnalyticsViewModel>(),
            ["vault"] = () => nav.NavigateAsync<VaultViewModel>(),
            ["assistant"] = () => nav.NavigateAsync<AssistantViewModel>(),
            ["settings"] = () => nav.NavigateAsync<SettingsViewModel>()
        };

        NavigateCommand = new AsyncRelayCommand<string>(async s => { if (s is not null) await GoToAsync(s); }, null, ui.Errors.Show);
        RefreshCommand = new AsyncRelayCommand(() => _nav.RefreshAsync(), null, ui.Errors.Show);
        PrimaryActionCommand = new RelayCommand(() => { var c = Page?.PrimaryAction; if (c?.CanExecute(null) == true) c.Execute(null); });
        LockVaultCommand = new RelayCommand(() => _vault.Lock("manual"));
        ToggleThemeCommand = new AsyncRelayCommand(ToggleThemeAsync, null, ui.Errors.Show);
        DismissToastCommand = new RelayCommand(toasts.Dismiss);

        _nav.Navigated += (_, _) => { Raise(nameof(Page)); ActiveSection = Page?.Section is { Length: > 0 } s ? s : _activeSection; };
        L.LanguageChanged += (_, _) => { RefreshNav(); Raise(nameof(AppTitle)); _ = SafeRefreshAsync(); };
        dialogs.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(DialogService.IsOpen)) Raise(nameof(IsDialogOpen)); };
    }

    public FTC.TeamDesk.Localization.ILocalizationService L { get; }
    public DialogService Dialogs { get; }
    public ToastService Toasts { get; }
    public IReadOnlyList<NavItem> NavItems { get; }
    public ObservableCollection<NavEntry> Nav { get; }
    public PageViewModel? Page => _nav.Current;
    public bool IsDialogOpen => Dialogs.IsOpen;
    public string AppTitle => L["App.Title"];

    public string ActiveSection { get => _activeSection; private set { if (SetProperty(ref _activeSection, value)) RefreshNav(); } }

    private void RefreshNav()
    {
        foreach (var n in Nav) { n.Label = L[n.Item.LocKey]; n.IsActive = n.Item.Section == _activeSection; }
    }

    public ICommand NavigateCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand PrimaryActionCommand { get; }
    public ICommand LockVaultCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand DismissToastCommand { get; }

    public async Task StartAsync()
    {
        var target = "dashboard";
        var prefs = _settings.Current;
        if (prefs.Startup == Core.Enums.StartupPage.LastVisited && prefs.LastPage is { } last && _routes.ContainsKey(last)) target = last;
        await GoToAsync(target);
    }

    public async Task GoToAsync(string section)
    {
        if (!_routes.TryGetValue(section, out var go)) return;
        ActiveSection = section;
        var prefs = _settings.Current;
        if (prefs.LastPage != section)
        {
            prefs.LastPage = section;
            try { await _settings.SaveAsync(prefs); } catch { /* non-critical */ }
        }
        await go();
    }

    /// <summary>Ctrl+1..8.</summary>
    public Task GoToIndexAsync(int index) => index >= 0 && index < NavItems.Count ? GoToAsync(NavItems[index].Section) : Task.CompletedTask;

    private async Task ToggleThemeAsync()
    {
        var prefs = _settings.Current;
        prefs.Theme = prefs.Theme == Core.Enums.ThemeMode.Dark ? Core.Enums.ThemeMode.Light : Core.Enums.ThemeMode.Dark;
        await _settings.SaveAsync(prefs);
        _theme.Apply(prefs.Theme);
    }

    private async Task SafeRefreshAsync() { try { await _nav.RefreshAsync(); } catch { } }
}


public sealed class NavEntry : ObservableObject
{
    private string _label = "";
    private bool _active;
    public NavEntry(NavItem item, int number) { Item = item; Number = number; }
    public NavItem Item { get; }
    public int Number { get; }
    public string Section => Item.Section;
    public string Glyph => Item.Glyph;
    public string Label { get => _label; set => SetProperty(ref _label, value); }
    public string Tooltip => $"Ctrl+{Number}";
    public bool IsActive { get => _active; set => SetProperty(ref _active, value); }
}
