using System.Collections.ObjectModel;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;
using System.Windows.Input;

namespace FTC.TeamDesk.UI.ViewModels.Analytics;

public sealed class AnalyticsViewModel : PageViewModel
{
    private readonly IAnalyticsService _analytics;
    private readonly IMemberService _members;
    private DateTime? _from, _to;
    private SelectOption<Guid?> _member = null!;
    private bool _suppress;

    public AnalyticsViewModel(UiContext ui, IAnalyticsService analytics, IMemberService members) : base(ui)
    {
        _analytics = analytics; _members = members;
        RefreshCommand = Cmd(() => ReloadAsync(CancellationToken.None));
        ResetCommand = Cmd(() => { _suppress = true; From = null; To = null; Member = MemberOptions[0]; _suppress = false; _ = RunAsync(() => ReloadAsync(CancellationToken.None)); });
    }

    public override string Section => "analytics";
    public override ICommand? RefreshCommand { get; }
    public ICommand ResetCommand { get; }

    public ObservableCollection<SelectOption<Guid?>> MemberOptions { get; } = new();
    public ObservableCollection<ChartDatum> MemberDistribution { get; } = new();
    public ObservableCollection<ChartDatum> Skills { get; } = new();
    public ObservableCollection<ChartDatum> ApplicationStatuses { get; } = new();
    public ObservableCollection<ChartDatum> BudgetUsage { get; } = new();
    public ObservableCollection<ChartDatum> SpendingOverTime { get; } = new();
    public ObservableCollection<ChartDatum> CategorySpending { get; } = new();
    public ObservableCollection<ChartDatum> CategoryPlanned { get; } = new();
    public ObservableCollection<LineSeriesData> Development { get; } = new();
    public ObservableCollection<LineSeriesData> SpendingLine { get; } = new();

    public DateTime? From { get => _from; set { if (SetProperty(ref _from, value) && !_suppress) _ = RunAsync(() => ReloadAsync(CancellationToken.None)); } }
    public DateTime? To { get => _to; set { if (SetProperty(ref _to, value) && !_suppress) _ = RunAsync(() => ReloadAsync(CancellationToken.None)); } }
    public SelectOption<Guid?> Member { get => _member; set { if (SetProperty(ref _member, value) && !_suppress) _ = RunAsync(() => ReloadAsync(CancellationToken.None)); } }
    public bool HasDevelopment => Development.Count > 0;

    public override Task LoadAsync(object? parameter, CancellationToken ct) => RunAsync(async () =>
    {
        _suppress = true;
        MemberOptions.Clear();
        MemberOptions.Add(new(null, L["Analytics.AllMembers"]));
        foreach (var m in await _members.ListAsync(new MemberFilter(), ct)) MemberOptions.Add(new(m.Id, m.FullName));
        _member = MemberOptions[0];
        Raise(nameof(Member));
        _suppress = false;
        await ReloadAsync(ct);
    });

    private async Task ReloadAsync(CancellationToken ct)
    {
        var d = await _analytics.GetAsync(new AnalyticsFilter(_from, _to?.Date.AddDays(1).AddTicks(-1), _member?.Value), ct);
        var cur = d.Budget.Currency;
        string M(decimal v) => Format.Money(v, cur);

        Fill(MemberDistribution, d.MemberDistribution.Select((n, i) => new ChartDatum(n.IsSystem ? L["MemberStatus." + n.Key] : n.Name ?? n.Key, n.Count, i)));
        Fill(Skills, d.SkillDistribution.Select((p, i) => new ChartDatum(p.Label, p.Value, i % 8)));
        Fill(ApplicationStatuses, d.ApplicationStatusDistribution.Select((p, i) =>
            new ChartDatum(Enum.TryParse<ApplicationStatus>(p.Label, out var st) ? Format.EnumText(st) : p.Label, p.Value, i)));
        Fill(BudgetUsage, new[]
        {
            new ChartDatum(L["Budget.Spent"], (double)d.Budget.TotalSpent, 0, M(d.Budget.TotalSpent)),
            new ChartDatum(L["Budget.CommittedNotSpent"], (double)d.Budget.CommittedNotSpent, 3, M(d.Budget.CommittedNotSpent)),
            new ChartDatum(L["Budget.Remaining"], (double)Math.Max(0, d.Budget.Remaining - d.Budget.CommittedNotSpent), 2, M(Math.Max(0, d.Budget.Remaining - d.Budget.CommittedNotSpent)))
        });
        Fill(SpendingOverTime, d.SpendingOverTime.Select(p => new ChartDatum(p.Label, p.Value, 0, M((decimal)p.Value))));
        SpendingLine.Clear();
        if (SpendingOverTime.Count > 0) SpendingLine.Add(new LineSeriesData(L["Analytics.SpendingOverTime"], 0, SpendingOverTime.ToList()));
        Fill(CategorySpending, d.CategorySpending.Select((c, i) => new ChartDatum(Format.CategoryName(c.CategoryKey, c.CategoryName, c.IsSystem), (double)c.Spent, i, M(c.Spent))));
        Fill(CategoryPlanned, d.CategorySpending.Select((c, i) => new ChartDatum(Format.CategoryName(c.CategoryKey, c.CategoryName, c.IsSystem), (double)c.Planned, i, M(c.Planned))));

        Development.Clear();
        var idx = 0;
        foreach (var s in d.MemberDevelopment)
        {
            var name = s.Key == "general" ? L["Team.Record.General"] : Format.AreaName(s.Key, s.CustomName, s.IsSystem);
            Development.Add(new LineSeriesData(name, idx % 8, s.Points.Select(p => new ChartDatum(p.Label, p.Value, idx % 8)).ToList()));
            idx++;
        }
        IsLoaded = true;
        Raise(nameof(HasDevelopment));
    }

    private static void Fill<T>(ObservableCollection<T> c, IEnumerable<T> items) { c.Clear(); foreach (var x in items) c.Add(x); }
}
