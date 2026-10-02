using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.DTOs;

/// <summary>
/// Forma exacta que espera el frontend nuevo (web/types/entities.ts), distinta
/// del modelo interno <see cref="Client"/> que todavía carga el doble nombre
/// legado (Nombre/Name, Telefono/Phone) y no tiene tenantId.
///
/// TenantId es un placeholder fijo ("default") hasta que exista multi-tenant
/// real en la base — se documenta así para que no se confunda con datos
/// migrados de verdad. Sacarlo cuando llegue el modelo de tenants (Sprint 1).
/// </summary>
public sealed record ClientDto(
    int ClientId,
    string TenantId,
    string Name,
    string Phone,
    string Email,
    string CreatedAt)
{
    public const string PlaceholderTenantId = "default";

    public static ClientDto FromModel(Client client) => new(
        ClientId: client.ClientId,
        TenantId: PlaceholderTenantId,
        Name: client.Name,
        Phone: client.Phone,
        Email: client.Email,
        CreatedAt: client.CreatedAt.ToString("O"));
}
