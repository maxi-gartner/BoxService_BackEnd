using System.Collections.Generic;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using Xunit;

namespace BoxService_BackEnd.UnitTests.Services;

public class BudgetServiceTests
{
    // These cases return before accessing persistence. Never configure a database here.
    [Fact]
    public void Create_WithInvalidVehicle_ReturnsVehicleErrorBeforeDetailsError()
    {
        var result = new BudgetService().Create(1, new BudgetCreateRequest
        {
            VehicleId = 0,
            Details = null!
        });

        Assert.False(result.ok);
        Assert.False(result.notFound);
        Assert.Equal("vehicleId is required", result.error);
        Assert.Null(result.result);
    }

    [Theory]
    [MemberData(nameof(InvalidDetails))]
    public void Create_WithInvalidDetails_ReturnsOriginalError(
        List<BudgetDetailRequest> details, string expectedError)
    {
        var result = new BudgetService().Create(1, new BudgetCreateRequest
        {
            VehicleId = 1,
            Details = details
        });

        Assert.False(result.ok);
        Assert.False(result.notFound);
        Assert.Equal(expectedError, result.error);
        Assert.Null(result.result);
    }

    public static IEnumerable<object[]> InvalidDetails()
    {
        yield return new object[] { null!, "details is required and must have at least one item" };
        yield return new object[] { new List<BudgetDetailRequest>(), "details is required and must have at least one item" };
        yield return new object[] { new List<BudgetDetailRequest> { null! }, "Each detail requires type labor or part" };
        foreach (var type in new[] { "", "LABOR", " labor ", "other" })
            yield return new object[]
            {
                new List<BudgetDetailRequest> { new() { Type = type, Description = "", Quantity = 0, UnitPrice = -1 } },
                "Each detail requires type labor or part"
            };
        foreach (var description in new[] { null, "", " \t " })
            yield return new object[]
            {
                new List<BudgetDetailRequest> { new() { Type = "labor", Description = description!, Quantity = 0, UnitPrice = -1 } },
                "Each detail requires a description"
            };
        foreach (var values in new[] { (0m, 1m), (-1m, 1m), (1m, -1m) })
            yield return new object[]
            {
                new List<BudgetDetailRequest> { new() { Type = "part", Description = "Item", Quantity = values.Item1, UnitPrice = values.Item2 } },
                "Quantity must be positive and unitPrice non-negative"
            };
        // The first invalid item wins, even if a later item has an invalid type.
        yield return new object[]
        {
            new List<BudgetDetailRequest>
            {
                new() { Type = "part", Description = "Item", Quantity = 0, UnitPrice = 1 },
                new() { Type = "invalid" }
            },
            "Quantity must be positive and unitPrice non-negative"
        };
    }
}
