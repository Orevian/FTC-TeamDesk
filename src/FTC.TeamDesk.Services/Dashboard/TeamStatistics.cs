using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Services;

/// <summary>Pure member statistics shared by the dashboard, analytics and AI summary.</summary>
public static class TeamStatistics
{
    private static readonly HashSet<string> InactiveStatusKeys = new() { "former", "rejected" };

    /// <summary>"Total members" is the active roster: everyone except Former Members and Rejected.</summary>
    public static bool IsActive(Member m) => m.Status is null || !InactiveStatusKeys.Contains(m.Status.Key);

    public static DashboardKpis Kpis(IReadOnlyList<Member> members, int applicationCount) => new(
        TotalMembers: members.Count(IsActive),
        AcceptedMembers: members.Count(m => m.Status?.Key == SystemKeys.AcceptedStatusKey),
        PendingCandidates: members.Count(m => m.Status?.Key is SystemKeys.PendingStatusKey or SystemKeys.CandidateStatusKey),
        Applications: applicationCount);

    public static IReadOnlyList<NamedCount> AreaDistribution(IReadOnlyList<Member> members, IReadOnlyList<Area> areas)
    {
        var active = members.Where(IsActive).ToList();
        return areas.OrderBy(a => a.SortOrder)
            .Select(a => new NamedCount(a.Key, a.Name, a.IsSystem, active.Count(m => m.MemberAreas.Any(x => x.AreaId == a.Id))))
            .Where(c => c.Count > 0)
            .OrderByDescending(c => c.Count)
            .ToList();
    }
}
