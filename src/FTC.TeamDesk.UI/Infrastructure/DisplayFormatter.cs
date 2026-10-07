using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Localization;

namespace FTC.TeamDesk.UI;

/// <summary>Turns domain values into localized display strings. Keeps localization out of the domain and out of XAML converters.</summary>
public sealed class DisplayFormatter
{
    private readonly ILocalizationService _loc;
    public DisplayFormatter(ILocalizationService loc) { _loc = loc; }

    public string StatusName(MemberStatus? s) => s is null ? "-" : _loc.NameOf("MemberStatus", s.Key, s.Name, s.IsSystem);
    public string AreaName(Area a) => _loc.NameOf("Area", a.Key, a.Name, a.IsSystem);
    public string AreaName(string key, string? name, bool isSystem) => _loc.NameOf("Area", key, name, isSystem);
    public string CategoryName(BudgetCategory? c) => c is null ? "-" : _loc.NameOf("Category", c.Key, c.Name, c.IsSystem);
    public string CategoryName(string key, string? name, bool isSystem) => _loc.NameOf("Category", key, name, isSystem);
    public string EnumText(Enum value) => _loc.Describe(value);
    public string Money(decimal amount, string currency) => _loc.FormatMoney(amount, currency);
    public string DateText(DateTime d) => _loc.FormatDate(d);
    public string DateTimeText(DateTime d) => _loc.FormatDateTime(d);

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
        return (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

    /// <summary>Stable 0-4 index used to tint avatars without images.</summary>
    public static int AvatarTone(string name)
    {
        unchecked { var h = 17; foreach (var c in name) h = h * 31 + c; return Math.Abs(h) % 5; }
    }

    public string ActivityText(ActivityLogEntry e)
    {
        var template = _loc.Get("Activity." + e.ActionType);
        if (template == "Activity." + e.ActionType) return e.Description.Length == 0 ? e.ActionType : $"{e.ActionType}: {e.Description}";
        return string.IsNullOrEmpty(e.Description) ? template.Replace(": {0}", "").Replace("{0}", "").Trim() : string.Format(_loc.Culture, template, e.Description);
    }

    public string Relative(DateTime utc)
    {
        var local = System.DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        var span = System.DateTime.Now - local;
        if (span.TotalSeconds < 60) return _loc["Time.JustNow"];
        if (span.TotalMinutes < 60) return _loc.Format("Time.MinutesAgo", (int)span.TotalMinutes);
        if (span.TotalHours < 24) return _loc.Format("Time.HoursAgo", (int)span.TotalHours);
        if (span.TotalDays < 2) return _loc["Time.Yesterday"];
        if (span.TotalDays < 7) return _loc.Format("Time.DaysAgo", (int)span.TotalDays);
        return _loc.FormatDate(local);
    }

    public string DueLabel(DateTime? due)
    {
        if (due is null) return "";
        var days = (due.Value.Date - System.DateTime.Today).Days;
        if (days < 0) return _loc.Format("Task.Overdue", -days);
        if (days == 0) return _loc["Task.Today"];
        if (days == 1) return _loc["Task.Tomorrow"];
        return _loc.FormatDate(due.Value);
    }
}
