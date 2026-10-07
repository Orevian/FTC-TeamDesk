using System.ComponentModel;
using FTC.TeamDesk.Localization;

namespace FTC.TeamDesk.UI.Localization;

/// <summary>
/// Bridges <see cref="ILocalizationService"/> to XAML bindings. Every {loc:Loc Key} binds to this indexer;
/// raising "Item[]" on a language change updates all bound texts instantly without recreating views.
/// </summary>
public sealed class LocalizationSource : INotifyPropertyChanged
{
    private static ILocalizationService? _service;
    public static LocalizationSource Instance { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;

    public static void Initialize(ILocalizationService service)
    {
        if (_service is not null) _service.LanguageChanged -= Instance.OnChanged;
        _service = service;
        service.LanguageChanged += Instance.OnChanged;
    }

    public string this[string key] => _service?.Get(key) ?? key;

    private void OnChanged(object? sender, EventArgs e)
    {
        var app = System.Windows.Application.Current;
        if (app is null || app.Dispatcher.CheckAccess()) Raise();
        else app.Dispatcher.BeginInvoke(new Action(Raise));
    }

    private void Raise() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
}
