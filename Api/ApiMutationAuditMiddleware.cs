using BoxService_BackEnd.Repositories;
using Microsoft.AspNetCore.Routing;

namespace BoxService_BackEnd.Api;

public sealed class ApiMutationAuditMiddleware(
    RequestDelegate next,
    ILogger<ApiMutationAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AuthAuditRepository auditRepository)
    {
        // Routing has already run. Never persist user-supplied path/query values.
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (route is null)
        {
            await next(context);
            return;
        }

        var completed = false;
        try
        {
            await next(context);
            completed = true;
        }
        finally
        {
            var status = context.Response.StatusCode;
            var denied = completed && status is 401 or 403 && !IsPublicRoute(route);
            var mutation = IsTrackedMethod(context.Request.Method) && !IsSensitiveAuthRoute(route);
            if (denied || mutation)
            {
                // Only validated principals supply actor identifiers, never request data.
                var authenticated = context.User.Identity?.IsAuthenticated == true;
                var actorId = authenticated && int.TryParse(context.User.FindFirst("sub")?.Value, out var actor)
                    ? actor : (int?)null;
                var tenantId = authenticated && int.TryParse(context.User.FindFirst("tenantId")?.Value, out var tenant)
                    ? tenant : (int?)null;
                var eventType = denied ? "access_denied"
                    : route.StartsWith("/users", StringComparison.OrdinalIgnoreCase)
                        || route.StartsWith("/tenants", StringComparison.OrdinalIgnoreCase)
                        ? "user_change" : "business_change";
                var outcome = denied ? status.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : completed && status < 400 ? "succeeded" : "failed";
                try
                {
                    auditRepository.Record(eventType, outcome, actorId, null, tenantId,
                        context.Connection.RemoteIpAddress?.ToString(), context.Request.Method, route);
                }
                catch (Exception)
                {
                    // Audit persistence is best effort and must never replace the HTTP result
                    // or mask an original exception. Do not log provider exceptions/messages.
                    logger.LogError(new EventId(50302, "AuditPersistenceFailed"),
                        "Audit persistence failed. EventType={EventType} Outcome={Outcome} Route={Route} TraceId={TraceId}",
                        eventType, outcome, route, context.TraceIdentifier);
                }
            }
        }
    }

    private static bool IsPublicRoute(string route) => route.ToLowerInvariant() is
        "/" or "/health" or "/auth/login" or "/auth/forgot-password"
        or "/auth/reset-password" or "/portal/auth/google";

    private static bool IsTrackedMethod(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private static bool IsSensitiveAuthRoute(string path) =>
        path.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/portal/auth/", StringComparison.OrdinalIgnoreCase)
        || (path.StartsWith("/users/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/reset-password", StringComparison.OrdinalIgnoreCase));
}
