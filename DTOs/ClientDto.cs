using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.DTOs;

/// <summary>
/// Forma exacta que espera el frontend (web/types/entities.ts), distinta
/// del modelo interno <see cref="Client"/> que todavía carga el doble nombre
/// legado (Nombre/Name, Telefono/Phone). TenantId va como string en el
/// contrato JSON (así era desde el placeholder "default") aunque en la
/// base ahora es un int real — se conserva el tipo para no romper los
/// tipos TypeScript existentes del lado del frontend.
/// </summary>
public sealed record ClientDto(
    int ClientId,
    string TenantId,
    string Name,
    string Phone,
    string Email,
    string CreatedAt)
{
    public static ClientDto FromModel(Client client) => new(
        ClientId: client.ClientId,
        TenantId: client.TenantId.ToString(),
        Name: client.Name,
        Phone: client.Phone,
        Email: client.Email,
        CreatedAt: client.CreatedAt.ToString("O"));
}
