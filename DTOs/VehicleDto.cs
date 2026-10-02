using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.DTOs;

/// <summary>
/// Forma exacta que espera el frontend nuevo (web/types/entities.ts) para un
/// vehículo. A diferencia de <see cref="ClientDto"/>, el modelo interno
/// <see cref="Vehicle"/> ya coincide campo a campo — este DTO solo agrega el
/// TenantId placeholder (ver ClientDto para la justificación).
/// </summary>
public sealed record VehicleDto(
    int VehicleId,
    string TenantId,
    int ClientId,
    string Brand,
    string Model,
    int? Year,
    string Plate,
    int CurrentMileage,
    string CreatedAt)
{
    public static VehicleDto FromModel(Vehicle vehicle) => new(
        VehicleId: vehicle.VehicleId,
        TenantId: ClientDto.PlaceholderTenantId,
        ClientId: vehicle.ClientId,
        Brand: vehicle.Brand,
        Model: vehicle.Model,
        Year: vehicle.Year,
        Plate: vehicle.Plate,
        CurrentMileage: vehicle.CurrentMileage,
        CreatedAt: vehicle.CreatedAt.ToString("O"));
}
