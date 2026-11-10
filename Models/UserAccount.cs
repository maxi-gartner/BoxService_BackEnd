using System;

namespace BoxService_BackEnd.Models
{
    /// <summary>
    /// Fila real de la tabla "usuarios" — reemplaza a la lista fija de
    /// Jwt:Users en appsettings.json. TenantId es null solo para
    /// "superadmin" (opera sobre todos los tenants, no pertenece a uno).
    /// </summary>
    public class UserAccount
    {
        public int UserId { get; set; }
        public int? TenantId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Role { get; set; } = string.Empty; // superadmin | dueno | empleado
        public bool Active { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    public class UserCreateRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        // Lo fija el servidor según quién llama (dueno siempre da de alta
        // "empleado"; superadmin puede elegir) — no se confía en lo que
        // mande el cliente para "dueno"/"superadmin".
        public string? Role { get; set; }
    }

    public class UserUpdateRequest
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public bool? Active { get; set; }
    }

    // POST /users/{id}/reset-password — soporte cuando un dueño/empleado
    // se olvida la contraseña. Mismo alcance que UserUpdateRequest: dueño
    // solo dentro de su propio tenant, superadmin sobre cualquiera.
    public class ResetPasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
    }
}
