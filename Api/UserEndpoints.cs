using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

// dueno ve/da de alta empleados de su propio tenant. superadmin ve
// TODOS los usuarios de TODOS los tenants (con el nombre del tenant
// para distinguirlos) — dar de alta en un tenant puntual es
// POST /tenants/{id}/users (ver TenantEndpoints.cs), no esta ruta.
public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapGet("/users", (HttpContext context, UserService service) =>
        {
            if (context.Role() == "superadmin")
                return Ok(service.ListAll().Select(UserDto.FromModel).ToArray());

            return Ok(service.ListByTenant(context.TenantId()).Select(u => UserDto.FromModel(u)).ToArray());
        });

        app.MapPost("/users", (UserCreateRequest request, HttpContext context, UserService service) => Write(() =>
        {
            var user = service.CreateEmployee(context.TenantId(), request);
            return Ok(UserDto.FromModel(user), 201);
        }));

        app.MapPatch("/users/{id:int}", (int id, UserUpdateRequest request, HttpContext context, UserService service) => Write(() =>
        {
            // superadmin: tenantId null = puede tocar cualquier usuario.
            // dueno: solo los de su propio tenant (el repo lo garantiza).
            var callerTenantId = context.Role() == "superadmin" ? (int?)null : context.TenantId();
            var updated = service.Update(id, callerTenantId, request);
            return updated is null ? Error(404, "User not found") : Ok(UserDto.FromModel(updated));
        }));

        // Soporte: el usuario se olvidó la contraseña. Mismo alcance que
        // el PATCH de arriba — el middleware ya bloquea a "empleado".
        app.MapPost("/users/{id:int}/reset-password", (int id, ResetPasswordRequest request, HttpContext context, UserService service) => Write(() =>
        {
            var callerTenantId = context.Role() == "superadmin" ? (int?)null : context.TenantId();
            var updated = service.ResetPassword(id, callerTenantId, request.NewPassword);
            return updated ? Ok(new { message = "Contraseña actualizada." }) : Error(404, "User not found");
        }));
    }
}
