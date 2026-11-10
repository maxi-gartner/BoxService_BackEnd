using BoxService_BackEnd.Models;
using Xunit;

namespace BoxService_BackEnd.UnitTests.Models;

public class BudgetWithDetailsTests
{
    [Fact]
    public void Total_WithoutDetails_ReturnsZero()
    {
        var budget = new BudgetWithDetails();

        var total = budget.Total;

        Assert.Equal(0m, total);
    }

    [Fact]
    public void Total_WithMultipleDetails_SumsStoredSubtotals()
    {
        // Total sums stored subtotals; it does not recalculate quantity * price.
        var budget = new BudgetWithDetails
        {
            Details =
            [
                new BudgetDetail { Quantity = 2m, UnitPrice = 100m, Subtotal = 180m },
                new BudgetDetail { Quantity = 1m, UnitPrice = 50m, Subtotal = 50m },
                new BudgetDetail { Subtotal = 0m }
            ]
        };

        var total = budget.Total;

        Assert.Equal(230m, total);
    }

    [Fact]
    public void Total_WithDecimalSubtotals_PreservesPrecision()
    {
        var budget = new BudgetWithDetails
        {
            Details =
            [
                new BudgetDetail { Subtotal = 0.10m },
                new BudgetDetail { Subtotal = 0.20m },
                new BudgetDetail { Subtotal = 12.345m }
            ]
        };

        var total = budget.Total;

        Assert.Equal(12.645m, total);
    }
}
