using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.DTOs;

public sealed record ServiceDto(
    int ServiceId,
    string TenantId,
    int VehicleId,
    string Date,
    int Mileage,
    string ServiceType,
    string Notes,
    int? NextMileage,
    string? NextDate)
{
    public static ServiceDto FromModel(Service service) => new(
        service.ServiceId,
        ClientDto.PlaceholderTenantId,
        service.VehicleId,
        service.Date.ToString("yyyy-MM-dd"),
        service.Mileage,
        service.ServiceType,
        service.Notes,
        service.NextMileage,
        service.NextDate?.ToString("yyyy-MM-dd"));
}
