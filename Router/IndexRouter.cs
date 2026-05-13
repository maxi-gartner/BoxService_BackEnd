using System;
using System.Net;

namespace BoxService_BackEnd.Router
{
    public class IndexRouter(
        VehicleRouter vehicles,
        BudgetRouter budgets,
        InvoiceRouter invoices,
        ServiceRouter services,
        HealthRouter health)
    {
        private readonly VehicleRouter _vehicles = vehicles;
        private readonly BudgetRouter _budgets = budgets;
        private readonly InvoiceRouter _invoices = invoices;
        private readonly ServiceRouter _services = services;
        private readonly HealthRouter _health = health;

        public void Route(HttpListenerContext context)
        {
            var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? "/";
            path = path.ToLower();

            try
            {
                // ── ROOT ─────────────────────────────

                if (path == "/")
                {
                    ResponseHelper.Ok(context.Response, new
                    {
                        message = "BoxService Backend",
                        status  = "online"
                    });

                    return;
                }

                // ── HEALTH ───────────────────────────

                if (path.StartsWith("/health"))
                {
                    _health.Route(context);
                    return;
                }

                // ── VEHICLES ─────────────────────────

                if (path.StartsWith("/api/vehiculos"))
                {
                    _vehicles.Route(context);
                    return;
                }

                // ── BUDGETS ──────────────────────────

                if (path.StartsWith("/api/budgets"))
                {
                    _budgets.Route(context);
                    return;
                }

                // ── INVOICES ─────────────────────────

                if (path.StartsWith("/api/invoices"))
                {
                    _invoices.Route(context);
                    return;
                }

                // ── SERVICES ─────────────────────────

                if (path.StartsWith("/api/services"))
                {
                    _services.Route(context);
                    return;
                }

                // ── 404 ──────────────────────────────

                ResponseHelper.NotFound(context.Response, "Route not found");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                ResponseHelper.InternalError(context.Response);
            }
        }
    }
}