using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly IMemberRepository _members;
    private readonly IApplicationRepository _applications;
    private readonly IBudgetService _budget;
    private readonly IBudgetRepository _budgetRepo;
    private readonly IActivityLogRepository _activity;
    private readonly ITaskRepository _tasks;

    public DashboardService(IMemberRepository members, IApplicationRepository applications, IBudgetService budget,
        IBudgetRepository budgetRepo, IActivityLogRepository activity, ITaskRepository tasks)
    {
        _members = members; _applications = applications; _budget = budget; _budgetRepo = budgetRepo; _activity = activity; _tasks = tasks;
    }

    public async Task<DashboardData> GetAsync(CancellationToken ct = default)
    {
        // Repositories use independent DbContexts, so the queries can safely run concurrently.
        var membersTask = _members.ListAsync(new MemberFilter(), ct);
        var areasTask = _members.ListAreasAsync(ct);
        var appCountTask = _applications.CountAsync(ct);
        var recentAppsTask = _applications.RecentAsync(5, ct);
        var budgetTask = _budget.GetSummaryAsync(ct);
        var purchasesTask = _budgetRepo.ListPurchasesAsync(ct);
        var activityTask = _activity.RecentAsync(8, ct);
        var tasksTask = _tasks.ListAsync(false, ct);
        await Task.WhenAll(membersTask, areasTask, appCountTask, recentAppsTask, budgetTask, purchasesTask, activityTask, tasksTask).ConfigureAwait(false);

        var members = membersTask.Result;
        return new DashboardData(
            Kpis: TeamStatistics.Kpis(members, appCountTask.Result),
            Budget: budgetTask.Result,
            RecentApplications: recentAppsTask.Result,
            RecentPurchases: purchasesTask.Result.Take(5).ToList(),
            RecentActivity: activityTask.Result,
            MemberDistribution: TeamStatistics.AreaDistribution(members, areasTask.Result),
            UpcomingTasks: tasksTask.Result.Take(6).ToList());
    }
}
