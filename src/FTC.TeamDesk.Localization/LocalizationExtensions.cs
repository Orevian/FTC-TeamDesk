namespace FTC.TeamDesk.Localization;

public static class LocalizationExtensions
{
    /// <summary>Localizes an enum value using the key "{EnumTypeName}.{Value}".</summary>
    public static string Describe(this ILocalizationService loc, Enum value) => loc.Get($"{value.GetType().Name}.{value}");

    /// <summary>System items are localized by key under <paramref name="prefix"/>; custom items show their own name.</summary>
    public static string NameOf(this ILocalizationService loc, string prefix, string key, string? customName, bool isSystem)
        => isSystem || string.IsNullOrWhiteSpace(customName) ? loc.Get($"{prefix}.{key}") : customName!;

    public static string FormatMoney(this ILocalizationService loc, decimal amount, string currency)
    {
        var symbol = currency.ToUpperInvariant() switch { "USD" => "$", "EUR" => "€", "TRY" => "₺", "GBP" => "£", _ => currency + " " };
        return symbol + amount.ToString("N2", loc.Culture);
    }

    public static string FormatDate(this ILocalizationService loc, DateTime date) => date.ToString("d", loc.Culture);

    public static string FormatDateTime(this ILocalizationService loc, DateTime date) => date.ToString("g", loc.Culture);
}
