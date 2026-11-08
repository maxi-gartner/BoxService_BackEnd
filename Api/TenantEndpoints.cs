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
            Ok(service.ListWithOwner().Select(row => new
            {
                tenantId = row.Tenant.TenantId,
                name = row.Tenant.Name,
                active = row.Tenant.Active,
                createdAt = row.Tenant.CreatedAt.ToString("O"),
                telefono = row.Tenant.Telefono,
                email = row.Tenant.Email,
                ownerName = row.OwnerName,
                ownerUsername = row.OwnerUsername,
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
            if (request.Active is null && request.Telefono is null && request.Email is null)
                return Error(400, "Nothing to update.");
            var updated = service.Update(id, request);
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

        // Control de pagos mensuales — historial de un taller puntual y
        // alta/corrección del pago de un período. GET /admin/payments
        // (ver AdminEndpoints.cs) es la vista general de todos los talleres.
        app.MapGet("/tenants/{id:int}/payments", (int id, TenantPaymentService service) =>
            Ok(service.GetHistory(id).Select(TenantPaymentDto.FromModel).ToArray()));

        app.MapPost("/tenants/{id:int}/payments", (int id, TenantPaymentCreateRequest request, TenantPaymentService service) => Write(() =>
            Ok(TenantPaymentDto.FromModel(service.Record(id, request)), 201)));
    }
}
