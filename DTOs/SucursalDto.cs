using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.DTOs;

public sealed record SucursalDto(
    int SucursalId,
    int TenantId,
    string Name,
    string? Address,
    bool Active,
    string CreatedAt)
{
    public static SucursalDto FromModel(Sucursal s) => new(
        SucursalId: s.SucursalId,
        TenantId: s.TenantId,
        Name: s.Name,
        Address: s.Address,
        Active: s.Active,
        CreatedAt: s.CreatedAt.ToString("O"));
}
