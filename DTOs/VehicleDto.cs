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
    string? Version,
    int? Year,
    string Plate,
    int CurrentMileage,
    string CreatedAt,
    string? ClientName = null)
{
    public static VehicleDto FromModel(Vehicle vehicle, string? clientName = null) => new(
        VehicleId: vehicle.VehicleId,
        TenantId: vehicle.TenantId.ToString(),
        ClientId: vehicle.ClientId,
        Brand: vehicle.Brand,
        Model: vehicle.Model,
        Version: vehicle.Version,
        Year: vehicle.Year,
        Plate: vehicle.Plate,
        CurrentMileage: vehicle.CurrentMileage,
        CreatedAt: vehicle.CreatedAt.ToString("O"),
        ClientName: clientName);
}
