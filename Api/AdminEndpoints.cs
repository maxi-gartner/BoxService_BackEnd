using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

// Solo superadmin — vista cruzada de clientes de todos los tenants, cada
// uno con el nombre del tenant al que pertenece. La contraparte de
// GET /clients (que sigue scoped al tenant del JWT para dueno/empleado).
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        app.MapGet("/admin/clients", (ClientService service) =>
            Ok(service.ListAllAdmin().Select(row => new
            {
                client = ClientDto.FromModel(row.Client),
                tenantName = row.TenantName,
            }).ToArray()));

        // Vista general: de un vistazo, qué talleres pagaron el mes
        // actual y cuál fue su último pago. El historial completo de uno
        // puntual es GET /tenants/{id}/payments (ver TenantEndpoints.cs).
        app.MapGet("/admin/payments", (TenantPaymentService service) =>
            Ok(service.GetOverview().Select(TenantPaymentStatusDto.FromModel).ToArray()));
    }
}
