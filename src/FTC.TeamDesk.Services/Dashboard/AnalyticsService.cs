using System.Globalization;
using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Calculations;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Services;

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IMemberRepository _members;
    private readonly IApplicationRepository _applications;
    private readonly IBudgetService _budget;
    private readonly IBudgetRepository _budgetRepo;
    private readonly ITaskRepository _tasks;

    public AnalyticsService(IMemberRepository members, IApplicationRepository applications, IBudgetService budget,
        IBudgetRepository budgetRepo, ITaskRepository tasks)
    {
        _members = members; _applications = applications; _budget = budget; _budgetRepo = budgetRepo; _tasks = tasks;
    }

    public async Task<AnalyticsData> GetAsync(AnalyticsFilter filter, CancellationToken ct = default)
    {
        var members = await _members.ListAsync(new MemberFilter(), ct).ConfigureAwait(false);
        var areas = await _members.ListAreasAsync(ct).ConfigureAwait(false);
        var apps = await _applications.ListAllAsync(ct).ConfigureAwait(false);
        var summary = await _budget.GetSummaryAsync(ct).ConfigureAwait(false);
        var purchases = await _budgetRepo.ListPurchasesAsync(ct).ConfigureAwait(false);
        var records = await _members.ListRecordsAsync(filter.MemberId, ct).ConfigureAwait(false);

        var skills = members.Where(TeamStatistics.IsActive)
            .SelectMany(m => m.MemberSkills.Select(s => s.Skill!.Name))
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ChartPoint(g.Key, g.Count()))
            .OrderByDescending(p => p.Value).ThenBy(p => p.Label).Take(12).ToList();

        var appStatus = Enum.GetValues<ApplicationStatus>()
            .Select(s => new ChartPoint(s.ToString(), apps.Count(a => a.Status == s))).ToList();

        var spent = purchases.Where(p => BudgetCalculator.CountsAsSpent(p.Status)
                                         && (filter.From is null || p.PurchaseDate >= filter.From.Value.Date)
                                         && (filter.To is null || p.PurchaseDate <= filter.To.Value.Date));
        var overTime = spent.GroupBy(p => new DateTime(p.PurchaseDate.Year, p.PurchaseDate.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new ChartPoint(g.Key.ToString("yyyy-MM", CultureInfo.InvariantCulture), (double)g.Sum(p => p.Total)))
            .ToList();

        return new AnalyticsData(
            TeamStatistics.AreaDistribution(members, areas),
            skills, appStatus, summary, overTime, summary.Categories,
            BuildDevelopment(members, areas, records, filter));
    }

    public async Task<TeamSummary> GetTeamSummaryAsync(CancellationToken ct = default)
    {
        var members = await _members.ListAsync(new MemberFilter(), ct).ConfigureAwait(false);
        var areas = await _members.ListAreasAsync(ct).ConfigureAwait(false);
        var apps = await _applications.ListAllAsync(ct).ConfigureAwait(false);
        var summary = await _budget.GetSummaryAsync(ct).ConfigureAwait(false);
        var tasks = await _tasks.ListAsync(false, ct).ConfigureAwait(false);
        return new TeamSummary(
            TeamStatistics.Kpis(members, apps.Count), summary, TeamStatistics.AreaDistribution(members, areas),
            Enum.GetValues<ApplicationStatus>().ToDictionary(s => s, s => apps.Count(a => a.Status == s)), tasks.Count);
    }

    private static IReadOnlyList<ChartSeries> BuildDevelopment(IReadOnlyList<Core.Entities.Member> members, IReadOnlyList<Core.Entities.Area> areas,
        IReadOnlyList<Core.Entities.PerformanceRecord> allRecords, AnalyticsFilter filter)
    {
        // Development is deliberately tracked per area: one series per area, never collapsed into a single score.
        var activeIds = members.Where(m => filter.MemberId is { } id ? m.Id == id : TeamStatistics.IsActive(m)).Select(m => m.Id).ToHashSet();
        var records = allRecords.Where(r => activeIds.Contains(r.MemberId))
            .Where(r => (filter.From is null || r.RecordedOn >= filter.From.Value.Date) && (filter.To is null || r.RecordedOn <= filter.To.Value.Date))
            .ToList();

        var series = new List<ChartSeries>();
        foreach (var group in records.GroupBy(r => r.AreaId))
        {
            var area = areas.FirstOrDefault(a => a.Id == group.Key);
            var points = group.GroupBy(r => new DateTime(r.RecordedOn.Year, r.RecordedOn.Month, 1)).OrderBy(g => g.Key)
                .Select(g => new ChartPoint(g.Key.ToString("yyyy-MM", CultureInfo.InvariantCulture), Math.Round(g.Average(r => r.Score), 1)))
                .ToList();
            series.Add(new ChartSeries(area?.Key ?? "general", area?.Name, area?.IsSystem ?? true, points));
        }
        return series.OrderBy(s => s.Key).ToList();
    }
}
