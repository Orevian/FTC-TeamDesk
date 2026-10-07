using FTC.TeamDesk.Core.Enums;

namespace FTC.TeamDesk.Core.Models;

public sealed record CategorySpending(
    Guid CategoryId, string CategoryKey, string? CategoryName, bool IsSystem,
    decimal Allocation, decimal Planned, decimal Spent);

public sealed record BudgetSummary(
    decimal TotalBudget,
    string Currency,
    decimal TotalPlanned,
    decimal TotalSpent,
    decimal Remaining,
    decimal Difference,
    decimal RequiredAdditionalBudget,
    decimal CommittedNotSpent,
    IReadOnlyList<CategorySpending> Categories);

public sealed record WhatIfResult(BudgetSummary Before, BudgetSummary After, decimal PurchaseTotal, decimal BudgetImpact);

public sealed class PurchaseEditModel
{
    public Guid? Id { get; set; }
    public string Product { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTime PurchaseDate { get; set; } = DateTime.Today;
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Planned;
    public string? Seller { get; set; }
    public string? Notes { get; set; }
}

public sealed class CategoryEditModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
}
