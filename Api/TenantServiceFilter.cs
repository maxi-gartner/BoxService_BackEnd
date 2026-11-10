using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

public interface ITenantServiceAccess
{
    bool Exists(int serviceId, int tenantId);
}

public sealed class TenantServiceAccess(ServicesService services) : ITenantServiceAccess
{
    public bool Exists(int serviceId, int tenantId) =>
        services.GetByIdForTenant(serviceId, tenantId) is not null;
}

// Aplica a TODAS las operaciones de inspección y fotos, antes de sus handlers.
public sealed class TenantServiceFilter(ITenantServiceAccess access) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (!int.TryParse(http.Request.RouteValues["serviceId"]?.ToString(), out var serviceId)
            || !int.TryParse(http.User.FindFirst("tenantId")?.Value, out var tenantId)
            || tenantId <= 0 || serviceId <= 0 || !access.Exists(serviceId, tenantId))
            return ValueTask.FromResult<object?>(Error(404, "Service no encontrado."));
        return next(context);
    }
}
