using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Dashboard;

public sealed record KpiItem(string Label, string Value, string? Hint = null, StatusTone Tone = StatusTone.Neutral);

public sealed record ListRow(string Title, string? Subtitle, string? Trailing, StatusTone Tone = StatusTone.Neutral, string? Badge = null);

public sealed class TaskRow : ObservableObject
{
    public TaskRow(TeamTask task, string due, bool overdue) { Task = task; DueText = due; IsOverdue = overdue; _done = task.IsCompleted; }
    private bool _done;
    public TeamTask Task { get; }
    public string Title => Task.Title;
    public string DueText { get; }
    public bool IsOverdue { get; }
    public bool IsDone { get => _done; set => SetProperty(ref _done, value); }
}

public sealed class DashboardViewModel : PageViewModel
{
    private readonly IDashboardService _dashboard;
    private readonly ITaskService _tasks;
    private readonly INavigationService _nav;
    private string _budgetText = "";
    private double _spentPercent;
    private string _budgetNote = "";

    public DashboardViewModel(UiContext ui, IDashboardService dashboard, ITaskService tasks, INavigationService nav) : base(ui)
    {
        _dashboard = dashboard; _tasks = tasks; _nav = nav;
        AddTaskCommand = Cmd(AddTaskAsync);
        EditTaskCommand = Cmd<TaskRow>(EditTaskAsync);
        DeleteTaskCommand = Cmd<TaskRow>(DeleteTaskAsync);
        ToggleTaskCommand = Cmd<TaskRow>(ToggleTaskAsync);
        RefreshCommand = Cmd(() => LoadCoreAsync(CancellationToken.None));
    }

    public override string Section => "dashboard";
    public override ICommand? PrimaryAction => AddTaskCommand;
    public override ICommand? RefreshCommand { get; }

    public ObservableCollection<KpiItem> Kpis { get; } = new();
    public ObservableCollection<ListRow> RecentApplications { get; } = new();
    public ObservableCollection<ListRow> RecentPurchases { get; } = new();
    public ObservableCollection<ListRow> Activity { get; } = new();
    public ObservableCollection<ChartDatum> MemberDistribution { get; } = new();
    public ObservableCollection<ChartDatum> BudgetSegments { get; } = new();
    public ObservableCollection<TaskRow> Tasks { get; } = new();

    public string BudgetText { get => _budgetText; private set => SetProperty(ref _budgetText, value); }
    public string BudgetNote { get => _budgetNote; private set => SetProperty(ref _budgetNote, value); }
    public double SpentPercent { get => _spentPercent; private set => SetProperty(ref _spentPercent, value); }
    public bool HasActivity => Activity.Count > 0;
    public bool HasTasks => Tasks.Count > 0;

    public ICommand AddTaskCommand { get; }
    public ICommand EditTaskCommand { get; }
    public ICommand DeleteTaskCommand { get; }
    public ICommand ToggleTaskCommand { get; }

    public override Task LoadAsync(object? parameter, CancellationToken ct) => RunAsync(() => LoadCoreAsync(ct));

    private async Task LoadCoreAsync(CancellationToken ct)
    {
        var d = await _dashboard.GetAsync(ct);
        var b = d.Budget;
        string M(decimal v) => Format.Money(v, b.Currency);

        Kpis.Clear();
        Kpis.Add(new(L["Dashboard.Kpi.Members"], d.Kpis.TotalMembers.ToString()));
        Kpis.Add(new(L["Dashboard.Kpi.Accepted"], d.Kpis.AcceptedMembers.ToString()));
        Kpis.Add(new(L["Dashboard.Kpi.Pending"], d.Kpis.PendingCandidates.ToString()));
        Kpis.Add(new(L["Dashboard.Kpi.Applications"], d.Kpis.Applications.ToString()));
        Kpis.Add(new(L["Dashboard.Kpi.TotalBudget"], M(b.TotalBudget)));
        Kpis.Add(new(L["Dashboard.Kpi.Spent"], M(b.TotalSpent), L.Format("Dashboard.Kpi.Planned", M(b.TotalPlanned))));
        Kpis.Add(new(L["Dashboard.Kpi.Remaining"], M(b.Remaining), null, b.Remaining < 0 ? StatusTone.Danger : StatusTone.Neutral));
        Kpis.Add(new(L["Dashboard.Kpi.Required"], M(b.RequiredAdditionalBudget), null,
            b.RequiredAdditionalBudget > 0 ? StatusTone.Warning : StatusTone.Neutral));

        BudgetText = $"{M(b.TotalSpent)} / {M(b.TotalBudget)}";
        SpentPercent = b.TotalBudget <= 0 ? 0 : Math.Min(100, (double)(b.TotalSpent / b.TotalBudget * 100));
        BudgetNote = b.RequiredAdditionalBudget > 0
            ? L.Format("Dashboard.Budget.OverPlan", M(b.RequiredAdditionalBudget))
            : L.Format("Dashboard.Budget.Headroom", M(b.Difference));
        BudgetSegments.Clear();
        BudgetSegments.Add(new(L["Budget.Spent"], (double)b.TotalSpent, 0, M(b.TotalSpent)));
        BudgetSegments.Add(new(L["Budget.CommittedNotSpent"], (double)b.CommittedNotSpent, 3, M(b.CommittedNotSpent)));
        BudgetSegments.Add(new(L["Budget.Unplanned"], (double)Math.Max(0, b.Difference), 2, M(Math.Max(0, b.Difference))));

        Fill(RecentApplications, d.RecentApplications.Select(a => new ListRow(a.ApplicantName,
            Format.Relative(a.SubmittedAt), null, StatusTones.For(a.Status), Format.EnumText(a.Status))));
        Fill(RecentPurchases, d.RecentPurchases.Select(p => new ListRow(p.Product, Format.CategoryName(p.Category),
            Format.Money(p.Total, b.Currency), StatusTones.For(p.Status), Format.EnumText(p.Status))));
        Fill(Activity, d.RecentActivity.Select(a => new ListRow(Format.ActivityText(a), Format.Relative(a.Timestamp), a.Actor == "User" ? null : a.Actor)));
        Fill(MemberDistribution, d.MemberDistribution.Select((n, i) =>
            new ChartDatum(n.IsSystem ? L["MemberStatus." + n.Key] : n.Name ?? n.Key, n.Count, i)));
        Fill(Tasks, d.UpcomingTasks.Select(t => new TaskRow(t, Format.DueLabel(t.DueDate), t.DueDate?.Date < DateTime.Today)));
        Raise(nameof(HasActivity), nameof(HasTasks));
        IsLoaded = true;
    }

    private static void Fill<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var i in items) target.Add(i);
    }

    private async Task AddTaskAsync()
    {
        var dlg = new TaskDialogViewModel(Ui, _tasks, null);
        if (await Ui.Dialogs.ShowAsync(dlg)) await LoadCoreAsync(CancellationToken.None);
    }

    private async Task EditTaskAsync(TaskRow? row)
    {
        if (row is null) return;
        var dlg = new TaskDialogViewModel(Ui, _tasks, row.Task);
        if (await Ui.Dialogs.ShowAsync(dlg)) await LoadCoreAsync(CancellationToken.None);
    }

    private async Task DeleteTaskAsync(TaskRow? row)
    {
        if (row is null) return;
        if (!await Ui.Dialogs.ConfirmAsync(L["Task.Delete.Title"], L.Format("Task.Delete.Message", row.Title), L["Common.Delete"], true)) return;
        await _tasks.DeleteAsync(row.Task.Id);
        await LoadCoreAsync(CancellationToken.None);
    }

    private async Task ToggleTaskAsync(TaskRow? row)
    {
        if (row is null) return;
        await _tasks.SetCompletedAsync(row.Task.Id, !row.IsDone);
        await LoadCoreAsync(CancellationToken.None);
    }
}
