using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.DTOs;

public sealed record BudgetDto(int BudgetId, string TenantId, string Number, string Date,
    string Status, string? Notes, int VehicleId, int? ServiceId,
    string? VehiclePlate = null, string? VehicleBrand = null, string? VehicleModel = null)
{
    public static BudgetDto FromModel(Budget value) => new(value.BudgetId,
        value.TenantId.ToString(), value.Number, value.Date, value.Status,
        value.Notes, value.VehicleId, value.ServiceId);

    public static BudgetDto FromModel(Budget value, string plate, string brand, string model) => new(
        value.BudgetId, value.TenantId.ToString(), value.Number, value.Date, value.Status,
        value.Notes, value.VehicleId, value.ServiceId, plate, brand, model);
}
