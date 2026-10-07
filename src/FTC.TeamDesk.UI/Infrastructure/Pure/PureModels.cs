using FTC.TeamDesk.Core.Enums;

namespace FTC.TeamDesk.UI;

/// <summary>One data point for charts and bar lists. <see cref="ColorIndex"/> selects Chart1..Chart8 from the active theme.</summary>
public sealed record ChartDatum(string Label, double Value, int ColorIndex = 0, string? Display = null)
{
    public string Text => Display ?? Value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
}

public sealed record LineSeriesData(string Name, int ColorIndex, IReadOnlyList<ChartDatum> Points);

/// <summary>Semantic tone of a status badge; the view maps it to theme colors (never color alone: the badge always has text).</summary>
public enum StatusTone { Neutral, Info, Success, Warning, Danger }

public static class StatusTones
{
    public static StatusTone For(ApplicationStatus s) => s switch
    {
        ApplicationStatus.Accepted => StatusTone.Success,
        ApplicationStatus.Rejected => StatusTone.Danger,
        ApplicationStatus.Reviewing => StatusTone.Info,
        ApplicationStatus.Maybe => StatusTone.Warning,
        _ => StatusTone.Neutral
    };

    public static StatusTone For(PurchaseStatus s) => s switch
    {
        PurchaseStatus.Received => StatusTone.Success,
        PurchaseStatus.Purchased => StatusTone.Info,
        PurchaseStatus.Ordered => StatusTone.Warning,
        PurchaseStatus.Cancelled => StatusTone.Danger,
        _ => StatusTone.Neutral
    };
}

public sealed record SelectOption<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record NavItem(string Section, string LocKey, string Glyph);
