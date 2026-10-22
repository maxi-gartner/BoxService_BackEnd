using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

// Solo superadmin — el middleware de Program.cs ya lo garantiza para
// todo lo que empieza con "/tenants".
public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        app.MapGet("/tenants", (TenantService service) =>
            Ok(service.List().Select(t => new
            {
                tenantId = t.TenantId,
                name = t.Name,
                active = t.Active,
                createdAt = t.CreatedAt.ToString("O"),
            }).ToArray()));

        app.MapPost("/tenants", (TenantCreateRequest request, TenantService service) => Write(() =>
        {
            var (tenant, owner) = service.Create(request);
            return Ok(new
            {
                tenantId = tenant.TenantId,
                name = tenant.Name,
                owner = new { userId = owner.UserId, username = owner.Username, name = owner.Name },
            }, 201);
        }));

        app.MapPatch("/tenants/{id:int}", (int id, TenantUpdateRequest request, TenantService service) => Write(() =>
        {
            if (request.Active is null) return Error(400, "Nothing to update.");
            var updated = service.SetActive(id, request.Active.Value);
            return updated ? Ok(new { message = "Tenant updated" }) : Error(404, "Tenant not found");
        }));

        // Alta de usuario en un tenant puntual — lo usa superadmin desde
        // su panel de administración de usuarios.
        app.MapPost("/tenants/{id:int}/users", (int id, UserCreateRequest request, UserService service) => Write(() =>
        {
            var user = service.CreateForTenant(id, request);
            return Ok(new { userId = user.UserId, username = user.Username, name = user.Name, role = user.Role }, 201);
        }));

        app.MapGet("/tenants/{id:int}/sucursales", (int id, SucursalService service) =>
            Ok(service.List(id).Select(SucursalDto.FromModel).ToArray()));
    }
}
