using FTC.TeamDesk.Core.Abstractions.Repositories;
using FTC.TeamDesk.Core.Common;
using FTC.TeamDesk.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FTC.TeamDesk.Data.Repositories;

public sealed class BudgetRepository : IBudgetRepository
{
    private readonly IDbContextFactory<TeamDeskDbContext> _factory;
    public BudgetRepository(IDbContextFactory<TeamDeskDbContext> factory) => _factory = factory;

    public async Task<Budget> GetBudgetAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var budget = await db.Budgets.AsNoTracking().FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (budget is not null) return budget;
        budget = new Budget();
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return budget;
    }

    public async Task SaveBudgetAsync(decimal totalAmount, string currency, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var budget = await db.Budgets.FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (budget is null) { budget = new Budget(); db.Budgets.Add(budget); }
        budget.TotalAmount = totalAmount;
        budget.Currency = currency;
        budget.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BudgetCategory>> ListCategoriesAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.BudgetCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<BudgetCategory> SaveCategoryAsync(BudgetCategory category, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var existing = await db.BudgetCategories.FirstOrDefaultAsync(c => c.Id == category.Id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            category.SortOrder = (await db.BudgetCategories.MaxAsync(c => (int?)c.SortOrder, ct).ConfigureAwait(false) ?? 0) + 1;
            db.BudgetCategories.Add(category);
            existing = category;
        }
        else
        {
            if (!existing.IsSystem) existing.Name = category.Name;
            existing.PlannedAmount = category.PlannedAmount;
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return existing;
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var cat = await db.BudgetCategories.FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false)
                  ?? throw new DomainException("Error.NotFound");
        if (cat.IsSystem) throw new DomainException("Error.SystemItemProtected");
        if (await db.Purchases.AnyAsync(p => p.CategoryId == id, ct).ConfigureAwait(false)) throw new DomainException("Error.CategoryInUse");
        db.BudgetCategories.Remove(cat);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Purchase>> ListPurchasesAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var list = await db.Purchases.AsNoTracking().Include(p => p.Category).ToListAsync(ct).ConfigureAwait(false);
        // Sorted in memory: SQLite cannot order by the TEXT-stored decimal/date reliably across cultures.
        return list.OrderByDescending(p => p.PurchaseDate).ThenByDescending(p => p.CreatedAt).ToList();
    }

    public async Task<Purchase?> GetPurchaseAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Purchases.AsNoTracking().Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<Purchase> SavePurchaseAsync(Purchase purchase, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var existing = await db.Purchases.FirstOrDefaultAsync(p => p.Id == purchase.Id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            purchase.Category = null;
            db.Purchases.Add(purchase);
            existing = purchase;
        }
        else
        {
            existing.Product = purchase.Product;
            existing.CategoryId = purchase.CategoryId;
            existing.UnitPrice = purchase.UnitPrice;
            existing.Quantity = purchase.Quantity;
            existing.PurchaseDate = purchase.PurchaseDate;
            existing.Status = purchase.Status;
            existing.Seller = purchase.Seller;
            existing.Notes = purchase.Notes;
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await db.Entry(existing).Reference(p => p.Category).LoadAsync(ct).ConfigureAwait(false);
        return existing;
    }

    public async Task DeletePurchaseAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Purchases.Where(p => p.Id == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }
}
