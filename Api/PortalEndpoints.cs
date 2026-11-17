using System.IdentityModel.Tokens.Jwt;
using BoxService_BackEnd.Auth;
using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

public static class PortalEndpoints
{
    public static void MapPortalEndpoints(this WebApplication app)
    {
        // Lado staff: genera (o renueva) la invitación de un cliente puntual.
        app.MapPost("/clients/{id:int}/portal-invite", (int id, HttpContext context, PortalService service) =>
        {
            var result = service.Invite(context.TenantId(), id);
            return result.ok ? Ok(result.result!) : Error(result.notFound ? 404 : 400, result.error);
        });

        // Pública: login/activación del portal con Google.
        app.MapPost("/portal/auth/google", async (PortalGoogleLoginRequest request, PortalAuthService authService) =>
        {
            var outcome = await authService.AuthenticateAsync(request.IdToken, request.InviteToken);
            return outcome.Success ? Ok(outcome.Result!) : Error(401, outcome.Error!);
        }).RequireRateLimiting("portal-login");

        // Rol customer: todo lo que el cliente puede ver de sus propios autos.
        app.MapGet("/portal/me", (
            HttpContext context,
            ClientRepository clientRepository,
            VehicleRepository vehicleRepository,
            BudgetRepository budgetRepository,
            ServicesService servicesService,
            WorkOrderService workOrderService) =>
        {
            var clientIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!int.TryParse(clientIdClaim, out var clientId))
                return Error(401, "Token inválido.");

            var client = clientRepository.GetById(clientId);
            if (client is null) return Error(404, "Cliente no encontrado.");

            var vehicles = vehicleRepository.GetByClientId(clientId, client.TenantId).Select(vehicle => new
            {
                vehicle = VehicleDto.FromModel(vehicle),
                // Historial completo, no solo lo último — "un registro de
                // lo que le hicimos al auto".
                budgets = budgetRepository.GetByVehicleId(vehicle.VehicleId, client.TenantId).Where(b => b.Status != "draft").Select(budget =>
                {
                    var details = budgetRepository.GetDetails(budget.BudgetId);
                    return new
                    {
                        budget = BudgetDto.FromModel(budget),
                        details,
                        total = details.Sum(d => d.Subtotal),
                    };
                }),
                services = servicesService.GetByVehicleId(vehicle.VehicleId, client.TenantId).Select(service => new
                {
                    service = ServiceDto.FromModel(service),
                    details = servicesService.GetDetails(service.ServiceId),
                }),
                workOrder = ToWorkOrderDto(workOrderService.GetLatestForVehicle(vehicle.VehicleId, client.TenantId)),
            });

            return Ok(new
            {
                client = new { client.ClientId, client.Name },
                vehicles,
            });
        });

        // Rol customer: "¿cómo va mi auto?" — queda pendiente hasta que
        // alguien del taller la marca como atendida desde Recordatorios.
        // Limitada a 1 por vehículo por hora (ver VehicleStatusInquiryRepository)
        // para que no se pueda hacer spam a fuerza de clicks.
        app.MapPost("/portal/vehicles/{id:int}/status-inquiry", (
            int id,
            HttpContext context,
            ClientRepository clientRepository,
            VehicleStatusInquiryService inquiryService) => Write(() =>
        {
            var clientIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!int.TryParse(clientIdClaim, out var clientId))
                return Error(401, "Token inválido.");

            var client = clientRepository.GetById(clientId);
            if (client is null) return Error(404, "Cliente no encontrado.");

            var (_, rateLimited, notFound, notReceived, result) = inquiryService.Create(client.TenantId, id, clientId);
            if (notFound) return Error(404, "Vehículo no encontrado.");
            if (notReceived) return Error(409, "Este vehículo todavía no fue recibido en el taller.");
            if (rateLimited) return Error(429, "Ya consultamos el estado de este vehículo hace menos de una hora. Probá de nuevo más tarde.");
            return Ok(new { inquiryId = result!.InquiryId }, 201);
        }));
    }

    private static object? ToWorkOrderDto(Models.WorkOrder? order) =>
        order is null ? null : new { status = order.Status, deliveredAt = order.DeliveredAt };
}
