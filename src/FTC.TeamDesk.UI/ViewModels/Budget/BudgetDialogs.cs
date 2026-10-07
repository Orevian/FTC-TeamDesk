using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Budget;

public sealed class BudgetSettingsDialogViewModel : DialogViewModel
{
    private readonly IBudgetService _budget;
    private string _total, _currency;

    public BudgetSettingsDialogViewModel(UiContext ui, IBudgetService budget, BudgetSummary current) : base(ui)
    {
        _budget = budget; _currency = current.Currency; _total = current.TotalBudget.ToString(ui.Loc.Culture);
    }

    public override string Title => L["Budget.Settings.Title"];
    public IReadOnlyList<string> Currencies { get; } = new[] { "USD", "EUR", "TRY", "GBP" };
    public string Total { get => _total; set => SetProperty(ref _total, value); }
    public string Currency { get => _currency; set => SetProperty(ref _currency, value); }

    protected override async Task<bool> OnConfirmAsync(CancellationToken ct)
    {
        if (!decimal.TryParse(_total, NumberStyles.Number, L.Culture, out var v) || v < 0) { Error = L["Budget.Error.InvalidNumbers"]; return false; }
        await _budget.SetBudgetAsync(v, _currency, ct);
        return true;
    }
}

public sealed class CategoryEditRow : ObservableObject
{
    private string _name = "", _amount = "0";
    public CategoryEditRow(BudgetCategory? c, string display, string amount)
    { Category = c; _name = display; _amount = amount; IsSystem = c?.IsSystem ?? false; }
    public BudgetCategory? Category { get; }
    public bool IsSystem { get; }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Amount { get => _amount; set => SetProperty(ref _amount, value); }
}

/// <summary>Edit planned allocation per category, add custom categories, delete unused custom ones.</summary>
public sealed class CategoryManagerDialogViewModel : DialogViewModel
{
    private readonly IBudgetService _budget;
    private string _newName = "";

    public CategoryManagerDialogViewModel(UiContext ui, IBudgetService budget) : base(ui)
    {
        _budget = budget;
        AddCommand = new RelayCommand(() =>
        {
            if (string.IsNullOrWhiteSpace(_newName)) return;
            Rows.Add(new CategoryEditRow(null, _newName.Trim(), "0"));
            NewName = "";
        });
        DeleteCommand = new AsyncRelayCommand<CategoryEditRow>(DeleteAsync, r => r is { IsSystem: false }, e => Error = ui.Errors.Describe(e));
    }

    public override string Title => L["Budget.Categories.Title"];
    public override double Width => 600;
    public ObservableCollection<CategoryEditRow> Rows { get; } = new();
    public string NewName { get => _newName; set => SetProperty(ref _newName, value); }
    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }

    public override async Task InitializeAsync()
    {
        foreach (var c in await _budget.ListCategoriesAsync())
            Rows.Add(new CategoryEditRow(c, Ui.Format.CategoryName(c), c.PlannedAmount.ToString(L.Culture)));
    }

    private async Task DeleteAsync(CategoryEditRow? row)
    {
        if (row is null) return;
        Error = null;
        if (row.Category is not null) await _budget.DeleteCategoryAsync(row.Category.Id);
        Rows.Remove(row);
    }

    protected override async Task<bool> OnConfirmAsync(CancellationToken ct)
    {
        foreach (var r in Rows)
        {
            if (!decimal.TryParse(r.Amount, NumberStyles.Number, L.Culture, out var amount) || amount < 0) { Error = L["Budget.Error.InvalidNumbers"]; return false; }
            // System categories keep their localized key; only the allocation changes. Custom categories can be renamed.
            var name = r.IsSystem ? (r.Category!.Name ?? r.Category.Key) : r.Name.Trim();
            await _budget.SaveCategoryAsync(new CategoryEditModel { Id = r.Category?.Id, Name = name, PlannedAmount = amount }, ct);
        }
        return true;
    }
}
