using System.Collections.ObjectModel;
using System.Globalization;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;
using FTC.TeamDesk.UI.Abstractions;
using FTC.TeamDesk.UI.Mvvm;

namespace FTC.TeamDesk.UI.ViewModels.Budget;

/// <summary>Add/edit purchase with a live "what if" preview computed by the deterministic budget service.</summary>
public sealed class PurchaseDialogViewModel : DialogViewModel
{
    private readonly IBudgetService _budget;
    private readonly Purchase? _existing;
    private string _product = "", _price = "0", _quantity = "1", _seller = "", _notes = "";
    private DateTime _date = DateTime.Today;
    private SelectOption<Guid>? _category;
    private SelectOption<PurchaseStatus> _status;
    private string _impact = "", _after = "";
    private bool _overBudget;
    private CancellationTokenSource? _cts;

    public PurchaseDialogViewModel(UiContext ui, IBudgetService budget, Purchase? existing) : base(ui)
    {
        _budget = budget; _existing = existing;
        foreach (var s in Enum.GetValues<PurchaseStatus>()) Statuses.Add(new(s, ui.Format.EnumText(s)));
        _status = Statuses[0];
        if (existing is not null)
        {
            _product = existing.Product; _price = existing.UnitPrice.ToString(CultureInfo.CurrentCulture); _quantity = existing.Quantity.ToString();
            _seller = existing.Seller ?? ""; _notes = existing.Notes ?? ""; _date = existing.PurchaseDate;
            _status = Statuses.First(s => s.Value == existing.Status);
        }
    }

    public override string Title => L[_existing is null ? "Budget.New" : "Budget.Edit"];
    public override double Width => 600;
    public ObservableCollection<SelectOption<Guid>> Categories { get; } = new();
    public ObservableCollection<SelectOption<PurchaseStatus>> Statuses { get; } = new();

    public string Product { get => _product; set { if (SetProperty(ref _product, value)) Preview(); } }
    public string UnitPrice { get => _price; set { if (SetProperty(ref _price, value)) Preview(); } }
    public string Quantity { get => _quantity; set { if (SetProperty(ref _quantity, value)) Preview(); } }
    public DateTime Date { get => _date; set => SetProperty(ref _date, value); }
    public string Seller { get => _seller; set => SetProperty(ref _seller, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public SelectOption<Guid>? Category { get => _category; set { if (SetProperty(ref _category, value)) Preview(); } }
    public SelectOption<PurchaseStatus> Status { get => _status; set { if (SetProperty(ref _status, value)) Preview(); } }
    public string ImpactText { get => _impact; private set => SetProperty(ref _impact, value); }
    public string AfterText { get => _after; private set => SetProperty(ref _after, value); }
    public bool IsOverBudget { get => _overBudget; private set => SetProperty(ref _overBudget, value); }

    public override async Task InitializeAsync()
    {
        foreach (var c in await _budget.ListCategoriesAsync()) Categories.Add(new(c.Id, Ui.Format.CategoryName(c)));
        Category = Categories.FirstOrDefault(c => c.Value == _existing?.CategoryId) ?? Categories.FirstOrDefault();
        Preview();
    }

    private bool TryBuild(out PurchaseEditModel model)
    {
        model = new PurchaseEditModel();
        if (_category is null) return false;
        if (!decimal.TryParse(_price, NumberStyles.Number, L.Culture, out var price) || price < 0) return false;
        if (!int.TryParse(_quantity, NumberStyles.Integer, L.Culture, out var qty) || qty < 1) return false;
        model = new PurchaseEditModel
        {
            Id = _existing?.Id, Product = _product.Trim(), CategoryId = _category.Value, UnitPrice = price, Quantity = qty,
            PurchaseDate = _date, Status = _status.Value, Seller = string.IsNullOrWhiteSpace(_seller) ? null : _seller.Trim(),
            Notes = string.IsNullOrWhiteSpace(_notes) ? null : _notes.Trim()
        };
        return true;
    }

    private void Preview()
    {
        _cts?.Cancel();
        if (!TryBuild(out var model)) { ImpactText = ""; AfterText = ""; return; }
        var cts = _cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                var r = await _budget.SimulatePurchaseAsync(model, cts.Token);
                if (cts.IsCancellationRequested) return;
                string M(decimal v) => Ui.Format.Money(v, r.After.Currency);
                ImpactText = L.Format("Budget.WhatIf.Total", M(r.PurchaseTotal));
                AfterText = r.After.RequiredAdditionalBudget > 0
                    ? L.Format("Budget.WhatIf.Over", M(r.After.RequiredAdditionalBudget))
                    : L.Format("Budget.WhatIf.After", M(r.After.Remaining), M(r.After.Difference));
                IsOverBudget = r.After.RequiredAdditionalBudget > 0 || r.After.Remaining < 0;
            }
            catch { /* preview is best-effort */ }
        });
    }

    protected override async Task<bool> OnConfirmAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_product)) { Error = L["Budget.Error.ProductRequired"]; return false; }
        if (_category is null) { Error = L["Budget.Error.CategoryRequired"]; return false; }
        if (!TryBuild(out var model)) { Error = L["Budget.Error.InvalidNumbers"]; return false; }
        await _budget.SavePurchaseAsync(model, ActivityActor.User, ct);
        return true;
    }
}
