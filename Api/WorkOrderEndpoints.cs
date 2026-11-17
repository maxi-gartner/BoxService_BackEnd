using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

public static class WorkOrderEndpoints
{
    public static void MapWorkOrderEndpoints(this WebApplication app)
    {
        var vehicles = app.MapGroup("/vehicles")
            .RequireAuthorization(policy => policy.RequireRole("dueno", "empleado"));

        // Recibir vehículo: abre una orden en en_espera. 409 si ya hay
        // una abierta para este vehículo.
        vehicles.MapPost("/{id:int}/work-orders", (int id, WorkOrderCreateRequest request, HttpContext context, WorkOrderService service) =>
        {
            var receivedBy = context.User.Identity?.Name ?? "";
            var (_, notFound, alreadyOpen, result) = service.Receive(context.TenantId(), id, request.Notes, receivedBy);
            if (notFound) return Error(404, "Vehicle not found.");
            if (alreadyOpen) return Error(409, "Este vehículo ya tiene una orden de trabajo abierta.");
            return Ok(new { orderId = result!.OrderId, status = result.Status }, 201);
        });

        // La más reciente de un vehículo puntual (abierta o la última
        // cerrada) — la usa el header del Taller.
        vehicles.MapGet("/{id:int}/work-order", (int id, HttpContext context, WorkOrderService service) =>
        {
            var order = service.GetLatestForVehicle(id, context.TenantId());
            return Ok(new
            {
                orderId = order?.OrderId,
                status = order?.Status,
                notes = order?.Notes,
                createdAt = order?.CreatedAt,
                startedAt = order?.StartedAt,
                finishedAt = order?.FinishedAt,
                deliveredAt = order?.DeliveredAt,
            });
        });

        var group = app.MapGroup("/work-orders")
            .RequireAuthorization(policy => policy.RequireRole("dueno", "empleado"));

        // Todas las abiertas del tenant (en_espera/en_proceso/terminado)
        // con datos de vehículo+cliente — el tablero.
        group.MapGet("", (HttpContext context, WorkOrderService service) => Ok(service.ListOpen(context.TenantId())));

        group.MapPatch("/{id:int}/status", (int id, WorkOrderStatusRequest request, HttpContext context, WorkOrderService service) =>
        {
            var deliveredBy = request.Status == "entregado" ? context.User.Identity?.Name : null;
            var (_, notFound, invalidTransition) = service.Advance(context.TenantId(), id, request.Status, request.Notes, deliveredBy);
            if (notFound) return Error(404, "Work order not found.");
            if (invalidTransition) return Error(409, "No se puede pasar a ese estado desde el paso actual.");
            return Ok(new { orderId = id, status = request.Status });
        });

        group.MapDelete("/{id:int}", (int id, HttpContext context, WorkOrderService service) =>
        {
            var (_, notFound, invalidState) = service.Cancel(context.TenantId(), id);
            if (notFound) return Error(404, "Work order not found.");
            if (invalidState) return Error(409, "Solo se puede cancelar una orden que todavía está en espera.");
            return Ok(new { cancelled = true });
        });
    }
}
