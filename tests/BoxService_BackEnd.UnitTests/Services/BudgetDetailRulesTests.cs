using System;
using System.Collections.Generic;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using Xunit;

namespace BoxService_BackEnd.UnitTests.Services;

public class BudgetDetailRulesTests
{
    [Theory]
    [MemberData(nameof(BudgetServiceTests.InvalidDetails), MemberType = typeof(BudgetServiceTests))]
    public void Validate_WithInvalidDetails_PreservesMessagesAndPriority(
        List<BudgetDetailRequest> details, string expectedError)
    {
        Assert.Equal(expectedError, BudgetDetailRules.Validate(details));
    }

    [Theory]
    [InlineData("labor", 0)]
    [InlineData("labor", 10)]
    [InlineData("part", 0)]
    [InlineData("part", 10)]
    public void Validate_WithValidDetails_AcceptsBothTypesAndZeroPrice(string type, int price)
    {
        var request = new BudgetDetailRequest
        {
            Type = type, Description = " Item ", Quantity = 0.25m, UnitPrice = price
        };

        Assert.Null(BudgetDetailRules.Validate([request]));
        Assert.Equal(" Item ", request.Description);
    }

    [Fact]
    public void ToDetails_PreservesOrderAndValues_TrimsDescriptionsWithoutMutatingInput()
    {
        var requests = new List<BudgetDetailRequest>
        {
            new() { Type = "part", Description = " \tRepuesto\r\n", Quantity = 1.25m, UnitPrice = 12.345m },
            new() { Type = "labor", Description = " Mano de obra ", Quantity = 2m, UnitPrice = 0m }
        };

        var details = BudgetDetailRules.ToDetails(requests);

        Assert.Equal(2, details.Count);
        Assert.Equal("part", details[0].Type);
        Assert.Equal("Repuesto", details[0].Description);
        Assert.Equal(1.25m, details[0].Quantity);
        Assert.Equal(12.345m, details[0].UnitPrice);
        Assert.Equal(15.43125m, details[0].Subtotal);
        Assert.Equal("labor", details[1].Type);
        Assert.Equal("Mano de obra", details[1].Description);
        Assert.Equal(2m, details[1].Quantity);
        Assert.Equal(0m, details[1].UnitPrice);
        Assert.Equal(0m, details[1].Subtotal);
        Assert.Equal(" \tRepuesto\r\n", requests[0].Description);
        Assert.Equal(" Mano de obra ", requests[1].Description);
        Assert.All(details, detail =>
        {
            Assert.Equal(0, detail.DetailId);
            Assert.Equal(0, detail.BudgetId);
        });
    }

    [Fact]
    public void Validate_DoesNotCalculateSubtotal_OverflowStillOccursDuringConversion()
    {
        var requests = new List<BudgetDetailRequest>
        {
            new() { Type = "labor", Description = "Item", Quantity = decimal.MaxValue, UnitPrice = 2m }
        };

        Assert.Null(BudgetDetailRules.Validate(requests));
        Assert.Throws<OverflowException>(() => BudgetDetailRules.ToDetails(requests));
    }
}
