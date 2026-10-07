using System.Windows.Data;
using System.Windows.Markup;

namespace FTC.TeamDesk.UI.Localization;

/// <summary>Usage: Text="{loc:Loc Nav.Dashboard}". Returns a live binding so the text follows language changes.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension() { }
    public LocExtension(string key) { Key = key; }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding("[" + Key + "]") { Source = LocalizationSource.Instance, Mode = BindingMode.OneWay };
        return binding.ProvideValue(serviceProvider);
    }
}
