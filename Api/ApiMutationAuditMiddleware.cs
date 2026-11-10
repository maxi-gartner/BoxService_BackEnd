using BoxService_BackEnd.Repositories;
using Npgsql;

namespace BoxService_BackEnd.Api;

public sealed class ApiMutationAuditMiddleware(
    RequestDelegate next,
    ILogger<ApiMutationAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, AuthAuditRepository auditRepository)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        if (!IsTrackedMethod(method) || IsSensitiveAuthRoute(path))
        {
            await next(context);
            return;
        }

        var outcome = "failed";
        try
        {
            await next(context);
            outcome = context.Response.StatusCode < StatusCodes.Status400BadRequest ? "succeeded" : "failed";
        }
        finally
        {
            var actorId = int.TryParse(context.User.FindFirst("sub")?.Value, out var parsedActorId)
                ? parsedActorId
                : (int?)null;
            var tenantId = int.TryParse(context.User.FindFirst("tenantId")?.Value, out var parsedTenantId)
                ? parsedTenantId
                : (int?)null;
            var eventType = path.StartsWith("/users", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/tenants", StringComparison.OrdinalIgnoreCase)
                ? "user_change"
                : "business_change";

            try
            {
                auditRepository.Record(
                    eventType,
                    outcome,
                    actorId,
                    null,
                    tenantId,
                    context.Connection.RemoteIpAddress?.ToString(),
                    method,
                    path);
            }
            catch (NpgsqlException exception)
            {
                logger.LogError(exception, "Failed to write API mutation audit event for {Method} {Path}.", method, path);
            }
        }
    }

    private static bool IsTrackedMethod(string method) =>
        HttpMethods.IsPost(method)
        || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method)
        || HttpMethods.IsDelete(method);

    private static bool IsSensitiveAuthRoute(string path) =>
        path.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/portal/auth/", StringComparison.OrdinalIgnoreCase)
        || (path.StartsWith("/users/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/reset-password", StringComparison.OrdinalIgnoreCase));
}
