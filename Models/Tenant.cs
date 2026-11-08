using System;

namespace BoxService_BackEnd.Models
{
    public class Tenant
    {
        public int TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
    }

    public class TenantCreateRequest
    {
        public string BusinessName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Email { get; set; }
    }

    public class TenantUpdateRequest
    {
        public bool? Active { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
    }

    // Fila de /tenants (superadmin) — el tenant con los datos del dueño
    // ya resueltos, así el panel no tiene que pegarle a /users aparte
    // para mostrar a quién pertenece cada taller.
    public sealed record TenantWithOwner(Tenant Tenant, string? OwnerName, string? OwnerUsername);
}
