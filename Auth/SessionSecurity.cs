using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Auth;

public static class SessionSecurity
{
    public const string VersionClaim = "credentialVersion";

    // No incluir el hash de contraseña en el JWT. Un cambio de contraseña
    // cambia esta huella y revoca las sesiones anteriores.
    public static string Version(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static bool IsValidStaff(ClaimsPrincipal principal, UserAccount? user, Tenant? tenant)
    {
        if (principal.Identity?.IsAuthenticated != true || user is null || !user.Active
            || string.IsNullOrEmpty(user.PasswordHash)) return false;
        if (!int.TryParse(principal.FindFirst("sub")?.Value, NumberStyles.None,
            CultureInfo.InvariantCulture, out var id) || id != user.UserId) return false;
        if (principal.FindFirst("role")?.Value != user.Role ||
            principal.FindFirst("name")?.Value != user.Username ||
            principal.FindFirst(VersionClaim)?.Value != Version(user.PasswordHash)) return false;
        if (user.Role == "superadmin")
            return user.TenantId is null && principal.FindFirst("tenantId") is null;
        return user.Role is "dueno" or "empleado" && user.TenantId is int tenantId
            && tenant is { Active: true } && tenant.TenantId == tenantId
            && int.TryParse(principal.FindFirst("tenantId")?.Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var tokenTenant) && tokenTenant == tenantId;
    }
}
