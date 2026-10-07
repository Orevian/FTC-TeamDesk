namespace FTC.TeamDesk.Core.Common;

/// <summary>Keys of the built-in (localized) statuses, areas and budget categories.</summary>
public static class SystemKeys
{
    public static readonly (string Key, string Color)[] MemberStatuses =
    {
        ("accepted", "#067647"), ("pending", "#B54708"), ("candidate", "#2456D6"),
        ("rejected", "#B42318"), ("former", "#6B7280")
    };

    public static readonly string[] Areas =
        { "software", "mechanical", "cad", "electronics", "communication", "design", "leadership" };

    public static readonly string[] BudgetCategories =
        { "robot", "electronics", "mechanical", "cad", "tools", "materials", "travel", "registration", "marketing", "other" };

    public const string AcceptedStatusKey = "accepted";
    public const string PendingStatusKey = "pending";
    public const string CandidateStatusKey = "candidate";
}
