using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

public sealed record ReminderPhoneRequest(string? Phone);

public static class ServiceReminderEndpoints
{
    public static void MapServiceReminderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/service-reminders")
            .RequireAuthorization(policy => policy.RequireRole("dueno", "empleado"));

        group.MapGet("", (HttpContext context, ServiceReminderRepository repo) =>
        {
            var today = ServiceReminderRules.Today();
            var items = repo.List(context.TenantId(), today);
            return Ok(new { items, today, pendingCount = items.Count(r => r.ContactedAt is null) });
        });

        group.MapPost("/{id:int}/contact", (int id, HttpContext context, ServiceReminderRepository repo) =>
        {
            var username = context.User.Identity?.Name ?? "";
            if (username.Length == 0 || username.Length > 150) return Error(401, "Usuario inválido.");
            return repo.Contact(context.TenantId(), id, username, ServiceReminderRules.Today())
                ? Ok(new { contacted = true }) : Error(404, "El recordatorio ya no está vigente o no pertenece al taller.");
        });

        group.MapPatch("/clients/{id:int}/phone", (int id, ReminderPhoneRequest request,
            HttpContext context, ServiceReminderRepository repo) =>
        {
            var digits = ServiceReminderRules.PhoneDigits(request.Phone);
            if (digits is null) return Error(400, "Ingresá el teléfono con código de país, por ejemplo +5493431234567.");
            return repo.UpdatePhone(context.TenantId(), id, "+" + digits)
                ? Ok(new { updated = true }) : Error(404, "Cliente no encontrado.");
        });
    }
}
