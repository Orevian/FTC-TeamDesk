using FTC.TeamDesk.Core.Calculations;
using FTC.TeamDesk.Core.Entities;
using FTC.TeamDesk.Core.Enums;
using Xunit;

namespace FTC.TeamDesk.Tests;

public class BudgetCalculatorTests
{
    private static (BudgetCategory[] cats, List<Purchase> purchases) Sample()
    {
        var electronics = new BudgetCategory { Key = "electronics", IsSystem = true };
        var robot = new BudgetCategory { Key = "robot", IsSystem = true };
        var purchases = new List<Purchase>
        {
            new() { Product = "Hub", CategoryId = electronics.Id, UnitPrice = 300m, Quantity = 1, Status = PurchaseStatus.Received },
            new() { Product = "Chassis", CategoryId = robot.Id, UnitPrice = 3900m, Quantity = 1, Status = PurchaseStatus.Purchased },
            new() { Product = "Servos", CategoryId = electronics.Id, UnitPrice = 30m, Quantity = 4, Status = PurchaseStatus.Planned },
            new() { Product = "Motors", CategoryId = electronics.Id, UnitPrice = 2280m, Quantity = 1, Status = PurchaseStatus.Ordered },
            new() { Product = "Dropped", CategoryId = robot.Id, UnitPrice = 999m, Quantity = 1, Status = PurchaseStatus.Cancelled },
        };
        return (new[] { electronics, robot }, purchases);
    }

    [Fact]
    public void RemainingMatchesSpecificationExample()
    {
        // Spec: total 10000, planned 6500, spent 4200 => remaining 5800
        var (cats, purchases) = Sample();
        var s = BudgetCalculator.Calculate(10000m, "USD", cats, purchases);
        Assert.Equal(4200m, s.TotalSpent);
        Assert.Equal(6600m, s.TotalPlanned); // 4200 spent + 120 planned + 2280 ordered
        Assert.Equal(5800m, s.Remaining);
        Assert.Equal(3400m, s.Difference);
        Assert.Equal(0m, s.RequiredAdditionalBudget);
        Assert.Equal(2400m, s.CommittedNotSpent);
    }

    [Fact]
    public void CancelledPurchasesNeverCount()
    {
        var (cats, purchases) = Sample();
        var s = BudgetCalculator.Calculate(10000m, "USD", cats, purchases);
        Assert.DoesNotContain(s.Categories, c => c.Planned == 999m);
        Assert.Equal(3900m, s.Categories.Single(c => c.CategoryKey == "robot").Spent);
    }

    [Fact]
    public void RequiredAdditionalBudgetWhenOverPlanned()
    {
        var (cats, purchases) = Sample();
        var s = BudgetCalculator.Calculate(5000m, "USD", cats, purchases);
        Assert.Equal(1600m, s.RequiredAdditionalBudget);
        Assert.Equal(-1600m, s.Difference);
        Assert.Equal(800m, s.Remaining);
    }

    [Fact]
    public void EmptyBudgetIsAllZero()
    {
        var s = BudgetCalculator.Calculate(0m, "USD", Array.Empty<BudgetCategory>(), Array.Empty<Purchase>());
        Assert.Equal(0m, s.TotalPlanned);
        Assert.Equal(0m, s.Remaining);
        Assert.Equal(0m, s.RequiredAdditionalBudget);
    }

    [Fact]
    public void SimulateDoesNotMutateOriginal()
    {
        var (cats, purchases) = Sample();
        var before = BudgetCalculator.Calculate(10000m, "USD", cats, purchases);
        var result = BudgetCalculator.Simulate(before, 120m, 1, PurchaseStatus.Planned);
        Assert.Equal(before.TotalPlanned + 120m, result.After.TotalPlanned);
        Assert.Equal(before.Remaining, result.After.Remaining); // planned items are not spent yet
        Assert.Equal(-120m, result.BudgetImpact);
        Assert.Equal(6600m, before.TotalPlanned);
    }

    [Fact]
    public void SimulatePurchasedReducesRemaining()
    {
        var (cats, purchases) = Sample();
        var before = BudgetCalculator.Calculate(10000m, "USD", cats, purchases);
        var result = BudgetCalculator.Simulate(before, 100m, 3, PurchaseStatus.Purchased);
        Assert.Equal(before.Remaining - 300m, result.After.Remaining);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(50, 200, 25)]
    [InlineData(1, 3, 33.3)]
    public void PercentHandlesZeroAndRounds(double part, double whole, double expected)
        => Assert.Equal((decimal)expected, BudgetCalculator.Percent((decimal)part, (decimal)whole));
}
