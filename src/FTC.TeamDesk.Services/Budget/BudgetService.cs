using System.Globalization;
using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Abstractions.Services;
using FTC.TeamDesk.Core.Calculations;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Services;

public sealed class BudgetService : IBudgetService
{
    private readonly IBudgetRepository _repo;
    private readonly IActivityLogService _log;

    public BudgetService(IBudgetRepository repo, IActivityLogService log)
    {
        _repo = repo; _log = log;
    }

    public async Task<BudgetSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var budget = await _repo.GetBudgetAsync(ct).ConfigureAwait(false);
        var categories = await _repo.ListCategoriesAsync(ct).ConfigureAwait(false);
        var purchases = await _repo.ListPurchasesAsync(ct).ConfigureAwait(false);
        return BudgetCalculator.Calculate(budget.TotalAmount, budget.Currency, categories, purchases);
    }

    public async Task SetBudgetAsync(decimal total, string currency, CancellationToken ct = default)
    {
        if (total < 0) throw new DomainException("Error.AmountNegative");
        var cur = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        if (cur.Length is < 2 or > 5) throw new DomainException("Error.InvalidCurrency");
        await _repo.SaveBudgetAsync(total, cur, ct).ConfigureAwait(false);
        await _log.LogAsync(ActivityActions.BudgetChanged, Money(total, cur), ActivityActor.User, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<BudgetCategory>> ListCategoriesAsync(CancellationToken ct = default) => _repo.ListCategoriesAsync(ct);

    public async Task<BudgetCategory> SaveCategoryAsync(CategoryEditModel model, CancellationToken ct = default)
    {
        if (model.PlannedAmount < 0) throw new DomainException("Error.AmountNegative");
        if (model.Id is null && string.IsNullOrWhiteSpace(model.Name)) throw new DomainException("Error.NameRequired");
        if (model.Id is null)
        {
            var existing = await _repo.ListCategoriesAsync(ct).ConfigureAwait(false);
            if (existing.Any(c => string.Equals(c.Name, model.Name.Trim(), StringComparison.OrdinalIgnoreCase))) throw new DomainException("Error.Duplicate");
        }
        var entity = new BudgetCategory
        {
            Id = model.Id ?? Guid.NewGuid(),
            Key = "custom-" + Guid.NewGuid().ToString("N")[..8],
            Name = string.IsNullOrWhiteSpace(model.Name) ? null : model.Name.Trim(),
            IsSystem = false,
            PlannedAmount = model.PlannedAmount
        };
        var saved = await _repo.SaveCategoryAsync(entity, ct).ConfigureAwait(false);
        await _log.LogAsync(ActivityActions.CategoryChanged, saved.Name ?? saved.Key, ActivityActor.User, ct).ConfigureAwait(false);
        return saved;
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        await _repo.DeleteCategoryAsync(id, ct).ConfigureAwait(false);
        await _log.LogAsync(ActivityActions.CategoryChanged, id.ToString(), ActivityActor.User, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<Purchase>> ListPurchasesAsync(CancellationToken ct = default) => _repo.ListPurchasesAsync(ct);

    public async Task<Purchase> SavePurchaseAsync(PurchaseEditModel model, ActivityActor actor = ActivityActor.User, CancellationToken ct = default)
    {
        await ValidateAsync(model, ct).ConfigureAwait(false);
        var isNew = model.Id is null;
        var purchase = new Purchase
        {
            Id = model.Id ?? Guid.NewGuid(),
            Product = model.Product.Trim(),
            CategoryId = model.CategoryId,
            UnitPrice = decimal.Round(model.UnitPrice, 2),
            Quantity = model.Quantity,
            PurchaseDate = model.PurchaseDate.Date,
            Status = model.Status,
            Seller = string.IsNullOrWhiteSpace(model.Seller) ? null : model.Seller.Trim(),
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim()
        };
        var saved = await _repo.SavePurchaseAsync(purchase, ct).ConfigureAwait(false);
        await _log.LogAsync(isNew ? ActivityActions.PurchaseAdded : ActivityActions.PurchaseEdited,
            $"{saved.Product} x{saved.Quantity}", actor, ct).ConfigureAwait(false);
        return saved;
    }

    public async Task DeletePurchaseAsync(Guid id, ActivityActor actor = ActivityActor.User, CancellationToken ct = default)
    {
        var existing = await _repo.GetPurchaseAsync(id, ct).ConfigureAwait(false) ?? throw new DomainException("Error.NotFound");
        await _repo.DeletePurchaseAsync(id, ct).ConfigureAwait(false);
        await _log.LogAsync(ActivityActions.PurchaseDeleted, existing.Product, actor, ct).ConfigureAwait(false);
    }

    public async Task<WhatIfResult> SimulatePurchaseAsync(PurchaseEditModel model, CancellationToken ct = default)
    {
        await ValidateAsync(model, ct).ConfigureAwait(false);
        var before = await GetSummaryAsync(ct).ConfigureAwait(false);
        return BudgetCalculator.Simulate(before, decimal.Round(model.UnitPrice, 2), model.Quantity, model.Status);
    }

    private async Task ValidateAsync(PurchaseEditModel m, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(m.Product)) throw new DomainException("Error.ProductRequired");
        if (m.Product.Trim().Length > 200) throw new DomainException("Error.TextTooLong");
        if (m.UnitPrice < 0) throw new DomainException("Error.AmountNegative");
        if (m.UnitPrice > 10_000_000m) throw new DomainException("Error.AmountTooLarge");
        if (m.Quantity is < 1 or > 100_000) throw new DomainException("Error.QuantityRange");
        var cats = await _repo.ListCategoriesAsync(ct).ConfigureAwait(false);
        if (cats.All(c => c.Id != m.CategoryId)) throw new DomainException("Error.CategoryRequired");
    }

    private static string Money(decimal amount, string currency) => $"{amount.ToString("0.##", CultureInfo.InvariantCulture)} {currency}";
}
