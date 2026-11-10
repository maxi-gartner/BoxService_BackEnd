using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Services;

internal static class BudgetDetailRules
{
    public static string? Validate(List<BudgetDetailRequest>? details)
    {
        if (details is null || details.Count == 0)
            return "details is required and must have at least one item";
        foreach (var detail in details)
        {
            if (detail is null || detail.Type is not ("labor" or "part"))
                return "Each detail requires type labor or part";
            if (string.IsNullOrWhiteSpace(detail.Description))
                return "Each detail requires a description";
            if (detail.Quantity <= 0 || detail.UnitPrice < 0)
                return "Quantity must be positive and unitPrice non-negative";
        }

        return null;
    }

    // Call after validation. Kept separate to preserve numbering before conversion in Create.
    public static List<BudgetDetail> ToDetails(List<BudgetDetailRequest> details) =>
        details.Select(d => new BudgetDetail
        {
            Type = d.Type,
            Description = d.Description.Trim(),
            Quantity = d.Quantity,
            UnitPrice = d.UnitPrice,
            Subtotal = d.Quantity * d.UnitPrice
        }).ToList();
}
