using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using FTC.TeamDesk.Core.Models;

namespace FTC.TeamDesk.Core.Calculations;

/// <summary>
/// Deterministic budget mathematics. This is the ONLY place money totals are computed;
/// the dashboard, analytics and AI tools all consume its results (AI never does arithmetic).
///
/// Definitions:
///   Spent      = purchases with status Purchased or Received
///   Planned    = every non-cancelled purchase (Planned, Ordered, Purchased, Received)
///   Remaining  = TotalBudget - Spent
///   Difference = TotalBudget - Planned          (negative when over-planned)
///   Required   = max(0, Planned - TotalBudget)  (additional budget needed to cover the plan)
/// </summary>
public static class BudgetCalculator
{
    public static bool CountsAsSpent(PurchaseStatus s) => s is PurchaseStatus.Purchased or PurchaseStatus.Received;

    public static bool CountsAsPlanned(PurchaseStatus s) => s != PurchaseStatus.Cancelled;

    public static BudgetSummary Calculate(
        decimal totalBudget,
        string currency,
        IEnumerable<BudgetCategory> categories,
        IEnumerable<Purchase> purchases)
    {
        var cats = categories.ToList();
        var list = purchases.ToList();

        decimal planned = 0m, spent = 0m;
        var perPlanned = new Dictionary<Guid, decimal>();
        var perSpent = new Dictionary<Guid, decimal>();

        foreach (var p in list)
        {
            var amount = p.UnitPrice * p.Quantity;
            if (CountsAsPlanned(p.Status))
            {
                planned += amount;
                perPlanned[p.CategoryId] = perPlanned.GetValueOrDefault(p.CategoryId) + amount;
            }
            if (CountsAsSpent(p.Status))
            {
                spent += amount;
                perSpent[p.CategoryId] = perSpent.GetValueOrDefault(p.CategoryId) + amount;
            }
        }

        var rows = cats
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategorySpending(
                c.Id, c.Key, c.Name, c.IsSystem, c.PlannedAmount,
                perPlanned.GetValueOrDefault(c.Id), perSpent.GetValueOrDefault(c.Id)))
            .ToList();

        return new BudgetSummary(
            TotalBudget: totalBudget,
            Currency: currency,
            TotalPlanned: planned,
            TotalSpent: spent,
            Remaining: totalBudget - spent,
            Difference: totalBudget - planned,
            RequiredAdditionalBudget: Math.Max(0m, planned - totalBudget),
            CommittedNotSpent: planned - spent,
            Categories: rows);
    }

    /// <summary>Computes the budget after hypothetically adding a purchase.</summary>
    public static WhatIfResult Simulate(BudgetSummary before, decimal unitPrice, int quantity, PurchaseStatus status)
    {
        var amount = unitPrice * quantity;
        var planned = before.TotalPlanned + (CountsAsPlanned(status) ? amount : 0m);
        var spent = before.TotalSpent + (CountsAsSpent(status) ? amount : 0m);
        var total = before.TotalBudget;
        var after = before with
        {
            TotalPlanned = planned,
            TotalSpent = spent,
            Remaining = total - spent,
            Difference = total - planned,
            RequiredAdditionalBudget = Math.Max(0m, planned - total),
            CommittedNotSpent = planned - spent
        };
        return new WhatIfResult(before, after, amount, -amount);
    }

    public static decimal Percent(decimal part, decimal whole) => whole <= 0m ? 0m : Math.Round(part / whole * 100m, 1);
}
