namespace BoxService_BackEnd.Api;

public static class HttpContextExtensions
{
    /// <summary>
    /// tenantId del JWT, para las rutas operativas del día a día del
    /// taller (clients/vehicles/budgets/services/invoices/catalog).
    /// Solo "dueno" y "empleado" llegan hasta acá — el middleware de
    /// Program.cs bloquea a "superadmin" antes, con un 403 claro, porque
    /// no tiene tenant propio (usa /tenants y /admin/* en cambio). Si
    /// esto tira, es un bug del middleware, no un caso a manejar acá.
    /// </summary>
    public static int TenantId(this HttpContext context)
    {
        var claim = context.User.FindFirst("tenantId")?.Value
            ?? throw new InvalidOperationException("El token no tiene tenantId.");
        return int.Parse(claim);
    }

    public static string Role(this HttpContext context) =>
        context.User.FindFirst("role")?.Value ?? "";

    /// <summary>null para superadmin (sin tenant propio).</summary>
    public static int? TenantIdOrNull(this HttpContext context)
    {
        var claim = context.User.FindFirst("tenantId")?.Value;
        return claim is null ? null : int.Parse(claim);
    }
}
