using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.DTOs;

public sealed record BudgetDto(int BudgetId, string TenantId, string Number, string Date,
    string Status, string? Notes, int VehicleId, int? ServiceId)
{
    public static BudgetDto FromModel(Budget value) => new(value.BudgetId,
        ClientDto.PlaceholderTenantId, value.Number, value.Date, value.Status,
        value.Notes, value.VehicleId, value.ServiceId);
}
