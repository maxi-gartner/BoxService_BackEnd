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
        app.MapPost("/clients/{id:int}/portal-invite", (int id, PortalService service) =>
        {
            var result = service.Invite(id);
            return result.ok ? Ok(result.result!) : Error(result.notFound ? 404 : 400, result.error);
        });

        // Pública: login/activación del portal con Google.
        app.MapPost("/portal/auth/google", async (PortalGoogleLoginRequest request, PortalAuthService authService) =>
        {
            var outcome = await authService.AuthenticateAsync(request.IdToken, request.InviteToken);
            return outcome.Success ? Ok(outcome.Result!) : Error(401, outcome.Error!);
        });

        // Rol customer: todo lo que el cliente puede ver de sus propios autos.
        app.MapGet("/portal/me", (
            HttpContext context,
            ClientRepository clientRepository,
            VehicleRepository vehicleRepository,
            BudgetRepository budgetRepository,
            ServicesService servicesService) =>
        {
            var clientIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!int.TryParse(clientIdClaim, out var clientId))
                return Error(401, "Token inválido.");

            var client = clientRepository.GetById(clientId);
            if (client is null) return Error(404, "Cliente no encontrado.");

            var vehicles = vehicleRepository.GetByClientId(clientId).Select(vehicle => new
            {
                vehicle = VehicleDto.FromModel(vehicle),
                // Historial completo, no solo lo último — "un registro de
                // lo que le hicimos al auto".
                budgets = budgetRepository.GetByVehicleId(vehicle.VehicleId).Select(budget =>
                {
                    var details = budgetRepository.GetDetails(budget.BudgetId);
                    return new
                    {
                        budget = BudgetDto.FromModel(budget),
                        details,
                        total = details.Sum(d => d.Subtotal),
                    };
                }),
                services = servicesService.GetByVehicleId(vehicle.VehicleId).Select(ServiceDto.FromModel),
            });

            return Ok(new
            {
                client = new { client.ClientId, client.Name },
                vehicles,
            });
        });
    }
}
