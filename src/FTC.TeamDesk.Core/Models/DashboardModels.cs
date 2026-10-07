using FTC.TeamDesk.Core.Entities;

namespace FTC.TeamDesk.Core.Models;

public sealed record NamedCount(string Key, string? Name, bool IsSystem, int Count);

public sealed record DashboardKpis(
    int TotalMembers, int AcceptedMembers, int PendingCandidates, int Applications);

public sealed record DashboardData(
    DashboardKpis Kpis,
    BudgetSummary Budget,
    IReadOnlyList<TeamApplication> RecentApplications,
    IReadOnlyList<Purchase> RecentPurchases,
    IReadOnlyList<ActivityLogEntry> RecentActivity,
    IReadOnlyList<NamedCount> MemberDistribution,
    IReadOnlyList<TeamTask> UpcomingTasks);

public sealed record ChartPoint(string Label, double Value);

/// <summary>A named series. Key identifies a system area (or "general"); CustomName is set for user-defined areas.</summary>
public sealed record ChartSeries(string Key, string? CustomName, bool IsSystem, IReadOnlyList<ChartPoint> Points);

public sealed record AnalyticsFilter(DateTime? From = null, DateTime? To = null, Guid? MemberId = null);

public sealed record AnalyticsData(
    IReadOnlyList<NamedCount> MemberDistribution,
    IReadOnlyList<ChartPoint> SkillDistribution,
    IReadOnlyList<ChartPoint> ApplicationStatusDistribution,
    BudgetSummary Budget,
    IReadOnlyList<ChartPoint> SpendingOverTime,
    IReadOnlyList<CategorySpending> CategorySpending,
    IReadOnlyList<ChartSeries> MemberDevelopment);
