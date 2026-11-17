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

        group.MapGet("", (HttpContext context, ServiceReminderRepository repo, VehicleStatusInquiryService inquiryService) =>
        {
            var today = ServiceReminderRules.Today();
            var items = repo.List(context.TenantId(), today);
            var statusInquiries = inquiryService.ListPending(context.TenantId());
            var pendingCount = items.Count(r => r.ContactedAt is null) + statusInquiries.Count;
            return Ok(new { items, statusInquiries, today, pendingCount });
        });

        // El cliente preguntó "¿cómo va mi auto?" desde el portal (ver
        // PortalEndpoints) — esto la saca de la lista de pendientes.
        group.MapPost("/status-inquiries/{id:int}/resolve", (int id, HttpContext context, VehicleStatusInquiryService inquiryService) =>
            inquiryService.Resolve(context.TenantId(), id)
                ? Ok(new { resolved = true })
                : Error(404, "La consulta ya no está pendiente o no pertenece al taller."));

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
