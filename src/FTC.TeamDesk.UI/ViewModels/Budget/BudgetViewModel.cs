using System.Collections.ObjectModel;
using System.Windows.Input;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;
using FTC.TeamDesk.UI.ViewModels.Dashboard;

namespace FTC.TeamDesk.UI.ViewModels.Budget;

public sealed class PurchaseRow
{
    public PurchaseRow(Purchase p, string currency, DisplayFormatter f)
    {
        Purchase = p; Id = p.Id; Product = p.Product; Category = f.CategoryName(p.Category);
        UnitPrice = f.Money(p.UnitPrice, currency); Quantity = p.Quantity; Total = f.Money(p.Total, currency);
        Date = f.DateText(p.PurchaseDate); Status = f.EnumText(p.Status); Tone = StatusTones.For(p.Status); Seller = p.Seller ?? "";
    }
    public Purchase Purchase { get; }
    public Guid Id { get; }
    public string Product { get; }
    public string Category { get; }
    public string UnitPrice { get; }
    public int Quantity { get; }
    public string Total { get; }
    public string Date { get; }
    public string Status { get; }
    public StatusTone Tone { get; }
    public string Seller { get; }
}

public sealed class CategoryRow
{
    public CategoryRow(CategorySpending c, string currency, DisplayFormatter f)
    {
        Id = c.CategoryId; Name = f.CategoryName(c.CategoryKey, c.CategoryName, c.IsSystem);
        Allocation = f.Money(c.Allocation, currency); Planned = f.Money(c.Planned, currency); Spent = f.Money(c.Spent, currency);
        Percent = c.Allocation <= 0 ? (c.Spent > 0 ? 100 : 0) : Math.Min(100, (double)(c.Spent / c.Allocation * 100));
        Over = c.Allocation > 0 && c.Planned > c.Allocation;
    }
    public Guid Id { get; }
    public string Name { get; }
    public string Allocation { get; }
    public string Planned { get; }
    public string Spent { get; }
    public double Percent { get; }
    public bool Over { get; }
}

public sealed class BudgetViewModel : PageViewModel
{
    private readonly IBudgetService _budget;
    private string _search = "";
    private SelectOption<PurchaseStatus?> _statusFilter = null!;
    private SelectOption<Guid?> _categoryFilter = null!;
    private BudgetSummary? _summary;
    private IReadOnlyList<Purchase> _all = Array.Empty<Purchase>();
    private PurchaseRow? _selected;

    public BudgetViewModel(UiContext ui, IBudgetService budget) : base(ui)
    {
        _budget = budget;
        StatusOptions.Add(new(null, L["Budget.Filter.AllStatuses"]));
        foreach (var s in Enum.GetValues<PurchaseStatus>()) StatusOptions.Add(new(s, Format.EnumText(s)));
        _statusFilter = StatusOptions[0];
        AddCommand = Cmd(AddAsync);
        EditCommand = Cmd<PurchaseRow>(EditAsync);
        DeleteCommand = Cmd<PurchaseRow>(DeleteAsync);
        EditBudgetCommand = Cmd(EditBudgetAsync);
        ManageCategoriesCommand = Cmd(ManageCategoriesAsync);
        RefreshCommand = Cmd(() => ReloadAsync());
    }

    public override string Section => "budget";
    public override ICommand? PrimaryAction => AddCommand;
    public override ICommand? RefreshCommand { get; }

    public ObservableCollection<PurchaseRow> Rows { get; } = new();
    public ObservableCollection<CategoryRow> Categories { get; } = new();
    public ObservableCollection<KpiItem> Figures { get; } = new();
    public ObservableCollection<SelectOption<PurchaseStatus?>> StatusOptions { get; } = new();
    public ObservableCollection<SelectOption<Guid?>> CategoryOptions { get; } = new();

    public string Search { get => _search; set { if (SetProperty(ref _search, value)) ApplyFilter(); } }
    public SelectOption<PurchaseStatus?> StatusFilter { get => _statusFilter; set { if (SetProperty(ref _statusFilter, value)) ApplyFilter(); } }
    public SelectOption<Guid?> CategoryFilter { get => _categoryFilter; set { if (SetProperty(ref _categoryFilter, value)) ApplyFilter(); } }
    public PurchaseRow? Selected { get => _selected; set => SetProperty(ref _selected, value); }
    public string FilteredTotal { get; private set; } = "";
    public bool IsEmpty => IsLoaded && Rows.Count == 0;
    public bool HasShortfall => _summary is { RequiredAdditionalBudget: > 0 };
    public string ShortfallText => _summary is null ? "" : L.Format("Budget.Shortfall", Format.Money(_summary.RequiredAdditionalBudget, _summary.Currency));

    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand EditBudgetCommand { get; }
    public ICommand ManageCategoriesCommand { get; }

    public override Task LoadAsync(object? parameter, CancellationToken ct) => RunAsync(() => ReloadAsync());

    private async Task ReloadAsync()
    {
        _summary = await _budget.GetSummaryAsync();
        _all = await _budget.ListPurchasesAsync();
        var cats = await _budget.ListCategoriesAsync();
        var keep = _categoryFilter?.Value;
        CategoryOptions.Clear();
        CategoryOptions.Add(new(null, L["Budget.Filter.AllCategories"]));
        foreach (var c in cats) CategoryOptions.Add(new(c.Id, Format.CategoryName(c)));
        _categoryFilter = CategoryOptions.FirstOrDefault(o => o.Value == keep) ?? CategoryOptions[0];
        Raise(nameof(CategoryFilter));

        var s = _summary; string M(decimal v) => Format.Money(v, s.Currency);
        Figures.Clear();
        Figures.Add(new(L["Budget.Total"], M(s.TotalBudget)));
        Figures.Add(new(L["Budget.Planned"], M(s.TotalPlanned)));
        Figures.Add(new(L["Budget.Spent"], M(s.TotalSpent)));
        Figures.Add(new(L["Budget.Remaining"], M(s.Remaining), null, s.Remaining < 0 ? StatusTone.Danger : StatusTone.Neutral));
        Figures.Add(new(L["Budget.Difference"], M(s.Difference), null, s.Difference < 0 ? StatusTone.Warning : StatusTone.Neutral));
        Figures.Add(new(L["Budget.Required"], M(s.RequiredAdditionalBudget), null, s.RequiredAdditionalBudget > 0 ? StatusTone.Warning : StatusTone.Neutral));

        Categories.Clear();
        foreach (var c in s.Categories) Categories.Add(new CategoryRow(c, s.Currency, Format));
        ApplyFilter();
        IsLoaded = true;
        Raise(nameof(HasShortfall), nameof(ShortfallText));
    }

    private void ApplyFilter()
    {
        if (_summary is null) return;
        IEnumerable<Purchase> q = _all;
        if (!string.IsNullOrWhiteSpace(_search))
            q = q.Where(p => p.Product.Contains(_search.Trim(), StringComparison.CurrentCultureIgnoreCase)
                          || (p.Seller?.Contains(_search.Trim(), StringComparison.CurrentCultureIgnoreCase) ?? false));
        if (_statusFilter?.Value is { } st) q = q.Where(p => p.Status == st);
        if (_categoryFilter?.Value is { } cat) q = q.Where(p => p.CategoryId == cat);
        var list = q.OrderByDescending(p => p.PurchaseDate).ToList();
        Rows.Clear();
        foreach (var p in list) Rows.Add(new PurchaseRow(p, _summary.Currency, Format));
        FilteredTotal = Format.Money(list.Where(p => p.Status != PurchaseStatus.Cancelled).Sum(p => p.Total), _summary.Currency);
        Raise(nameof(FilteredTotal), nameof(IsEmpty));
    }

    private async Task AddAsync()
    {
        if (await Ui.Dialogs.ShowAsync(new PurchaseDialogViewModel(Ui, _budget, null))) { Ui.Toasts.Show(L["Budget.Saved"], ToastKind.Success); await ReloadAsync(); }
    }

    private async Task EditAsync(PurchaseRow? row)
    {
        if (row is null) return;
        if (await Ui.Dialogs.ShowAsync(new PurchaseDialogViewModel(Ui, _budget, row.Purchase))) { Ui.Toasts.Show(L["Budget.Saved"], ToastKind.Success); await ReloadAsync(); }
    }

    private async Task DeleteAsync(PurchaseRow? row)
    {
        if (row is null) return;
        if (!await Ui.Dialogs.ConfirmAsync(L["Budget.Delete.Title"], L.Format("Budget.Delete.Message", row.Product), L["Common.Delete"], true)) return;
        await _budget.DeletePurchaseAsync(row.Id);
        await ReloadAsync();
    }

    private async Task EditBudgetAsync()
    {
        if (_summary is null) return;
        if (await Ui.Dialogs.ShowAsync(new BudgetSettingsDialogViewModel(Ui, _budget, _summary))) await ReloadAsync();
    }

    private async Task ManageCategoriesAsync()
    {
        await Ui.Dialogs.ShowAsync(new CategoryManagerDialogViewModel(Ui, _budget));
        await ReloadAsync();
    }
}
