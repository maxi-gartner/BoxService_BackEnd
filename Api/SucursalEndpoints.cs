using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

// Lectura: dueno y empleado (hace falta para elegir sucursal al cargar
// un service). Escritura: solo dueno — lo gatea el middleware de
// Program.cs, igual que ya hace con /catalog.
public static class SucursalEndpoints
{
    public static void MapSucursalEndpoints(this WebApplication app)
    {
        app.MapGet("/sucursales", (HttpContext context, SucursalService service) =>
            Ok(service.List(context.TenantId()).Select(SucursalDto.FromModel).ToArray()));

        app.MapPost("/sucursales", (SucursalCreateRequest request, HttpContext context, SucursalService service) => Write(() =>
            Ok(SucursalDto.FromModel(service.Create(context.TenantId(), request)), 201)));

        app.MapPatch("/sucursales/{id:int}", (int id, SucursalUpdateRequest request, HttpContext context, SucursalService service) => Write(() =>
        {
            var updated = service.Update(id, context.TenantId(), request);
            return updated is null ? Error(404, "Sucursal not found") : Ok(SucursalDto.FromModel(updated));
        }));
    }
}
