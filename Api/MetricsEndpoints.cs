using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

// Resumen de cómo viene el mes/año para el tenant del que llama (dueño o
// empleado). Superadmin no tiene tenant propio, así que ni pisa esta
// ruta — el middleware de Program.cs ya lo bloquea como cualquier otra
// ruta operativa.
public static class MetricsEndpoints
{
    public static void MapMetricsEndpoints(this WebApplication app)
    {
        app.MapGet("/metrics", (HttpContext context, MetricsService service) =>
            Ok(service.GetMetrics(context.TenantId())));
    }
}
