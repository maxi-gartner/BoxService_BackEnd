using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.DTOs;

public sealed record InvoiceDto(int InvoiceId, string TenantId, string Number, string Date,
    decimal Total, string Status, int ServiceId, int? BudgetId,
    int? VehicleId = null, string? VehiclePlate = null, string? VehicleBrand = null, string? VehicleModel = null)
{
    public static InvoiceDto FromModel(Invoice value) => new(value.InvoiceId,
        value.TenantId.ToString(), value.Number, value.Date, value.Total,
        value.Status, value.ServiceId, value.BudgetId);

    public static InvoiceDto FromModel(Invoice value, int vehicleId, string plate, string brand, string model) => new(
        value.InvoiceId, value.TenantId.ToString(), value.Number, value.Date, value.Total,
        value.Status, value.ServiceId, value.BudgetId, vehicleId, plate, brand, model);
}
