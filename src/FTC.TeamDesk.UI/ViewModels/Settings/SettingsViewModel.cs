using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Settings;

/// <summary>Base for one settings tab. Sections handle their own persistence and errors.</summary>
public abstract class SettingsSection : ObservableObject
{
    protected SettingsSection(UiContext ui) { Ui = ui; }
    protected UiContext Ui { get; }
    public FTC.TeamDesk.Localization.ILocalizationService L => Ui.Loc;
    public abstract Task LoadAsync(CancellationToken ct);
    protected ICommand Cmd(Func<Task> action, Func<bool>? can = null) => new AsyncRelayCommand(action, can, Ui.Errors.Show);
    protected ICommand Cmd<T>(Func<T?, Task> action, Func<T?, bool>? can = null) => new AsyncRelayCommand<T>(action, can, Ui.Errors.Show);
}

public sealed class SettingsViewModel : PageViewModel
{
    private string _tab = "general";

    public SettingsViewModel(UiContext ui, GeneralSettingsViewModel general, AiSettingsViewModel ai, GoogleSettingsViewModel google,
        SecuritySettingsViewModel security, DataSettingsViewModel data, AboutViewModel about) : base(ui)
    {
        General = general; Ai = ai; Google = google; Security = security; Data = data; About = about;
        Tabs = new SelectOption<string>[]
        {
            new("general", L["Settings.General"]), new("ai", L["Settings.Ai"]), new("google", L["Settings.Google"]),
            new("security", L["Settings.Security"]), new("data", L["Settings.Data"]), new("about", L["Settings.About"])
        };
        SelectTabCommand = Cmd<string>(t => { if (t is not null) Tab = t; });
    }

    public override string Section => "settings";
    public GeneralSettingsViewModel General { get; }
    public AiSettingsViewModel Ai { get; }
    public GoogleSettingsViewModel Google { get; }
    public SecuritySettingsViewModel Security { get; }
    public DataSettingsViewModel Data { get; }
    public AboutViewModel About { get; }
    public IReadOnlyList<SelectOption<string>> Tabs { get; }
    public ICommand SelectTabCommand { get; }

    public string Tab { get => _tab; set { if (SetProperty(ref _tab, value)) Raise(nameof(IsGeneral), nameof(IsAi), nameof(IsGoogle), nameof(IsSecurity), nameof(IsData), nameof(IsAbout)); } }
    public bool IsGeneral => _tab == "general";
    public bool IsAi => _tab == "ai";
    public bool IsGoogle => _tab == "google";
    public bool IsSecurity => _tab == "security";
    public bool IsData => _tab == "data";
    public bool IsAbout => _tab == "about";

    public override Task LoadAsync(object? parameter, CancellationToken ct) => RunAsync(async () =>
    {
        if (parameter is string t && Tabs.Any(x => x.Value == t)) Tab = t;
        await Task.WhenAll(General.LoadAsync(ct), Ai.LoadAsync(ct), Google.LoadAsync(ct), Security.LoadAsync(ct), Data.LoadAsync(ct), About.LoadAsync(ct));
        IsLoaded = true;
    });
}
