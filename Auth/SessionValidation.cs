using BoxService_BackEnd.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace BoxService_BackEnd.Auth;

public static class SessionValidation
{
    public static Task Validate(TokenValidatedContext context)
    {
        var principal = context.Principal!;
        var services = context.HttpContext.RequestServices;
        if (!int.TryParse(principal.FindFirst("sub")?.Value, out var id) || id <= 0)
        {
            context.Fail("Sesión inválida.");
            return Task.CompletedTask;
        }

        var tenants = services.GetRequiredService<TenantRepository>();
        if (principal.FindFirst("role")?.Value == "customer")
        {
            var client = services.GetRequiredService<ClientRepository>().GetById(id);
            var version = services.GetRequiredService<PortalAccessRepository>().GetSessionVersion(id);
            if (client is null || tenants.GetById(client.TenantId) is not { Active: true }
                || version is null || principal.FindFirst("portalSession")?.Value != version)
                context.Fail("El acceso al portal dejó de estar vigente.");
        }
        else
        {
            var user = services.GetRequiredService<UserRepository>().GetById(id);
            var tenant = user?.TenantId is int tenantId ? tenants.GetById(tenantId) : null;
            if (!SessionSecurity.IsValidStaff(principal, user, tenant))
                context.Fail("El acceso o la contraseña cambiaron. Volvé a iniciar sesión.");
        }
        return Task.CompletedTask;
    }
}
