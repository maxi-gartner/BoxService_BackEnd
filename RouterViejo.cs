/* using System;
using System.Net;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd
{
    public class Router
    {
        private readonly HealthController   _health;
        private readonly BudgetController   _budgets;
        private readonly InvoiceController  _invoices;
        private readonly VehicleController  _vehicles;
        private readonly ServicesController _services;

        // Cristhian descomenta cuando conecte su controller:
        // private readonly ClientController _clients;

        public Router(VehicleController vehicleController)
        {
            _health   = new HealthController();
            _budgets  = new BudgetController();
            _invoices = new InvoiceController();
            _vehicles = vehicleController;
            _services = new ServicesController();
        }

        public void Route(HttpListenerContext context)
        {
            var request  = context.Request;
            var response = context.Response;
            var method   = request.HttpMethod.ToUpper();
            var path     = request.Url?.AbsolutePath.TrimEnd('/') ?? "/";

            try
            {
                // ── ROOT ─────────────────────────────────────────────
                if (method == "GET" && path == "/")
                {
                    ResponseHelper.Ok(response, new { message = "BoxService Backend", status = "online", version = "1.0.0" });
                    return;
                }

                // ── HEALTH ───────────────────────────────────────────
                if (method == "GET" && path == "/health")
                {
                    _health.GetHealth(response);
                    return;
                }

                // ── CLIENTS (Cristhian — descomentar al conectar) ────
                // if (path.StartsWith("/api/clients"))
                // {
                //     if (method == "GET"  && path == "/api/clients")          { _clients.GetAll(response);               return; }
                //     if (method == "GET"  && path.Contains("/vehicles"))       { _clients.GetVehicles(request, response); return; }
                //     if (method == "GET"  && path.StartsWith("/api/clients/")) { _clients.GetById(request, response);     return; }
                //     if (method == "POST" && path == "/api/clients")           { _clients.Create(request, response);      return; }
                //     ResponseHelper.NotFound(response, "Client route not found");
                //     return;
                // }

                // ── VEHICLES (Leo) ───────────────────────────────────
                if (path.StartsWith("/api/vehiculos"))
                {
                    if      (method == "GET"  && path == "/api/vehiculos")         _vehicles.GetAll(response);
                    else if (method == "GET"  && path.Contains("/buscar"))          _vehicles.SearchByPlate(request, response);
                    else if (method == "GET"  && path.EndsWith("/historial"))       _vehicles.GetHistory(response);
                    else if (method == "POST" && path == "/api/vehiculos")          _vehicles.Create(request, response);
                    else if (TryGetId(path, "/api/vehiculos/", out int vid))        _vehicles.GetById(request, response, vid);
                    else ResponseHelper.NotFound(response, "Vehicle route not found");
                    return;
                }

                // ── BUDGETS (Maxi) ───────────────────────────────────
                if (path.StartsWith("/api/budgets"))
                {
                    if      (method == "GET"  && path == "/api/budgets")            _budgets.GetAll(response);
                    else if (method == "POST" && path == "/api/budgets")            _budgets.Create(request, response);
                    else if (method == "PUT"  && path.Contains("/status"))          _budgets.UpdateStatus(request, response);
                    else if (method == "POST" && path.Contains("/approve"))         _budgets.Approve(request, response);
                    else if (method == "GET"  && path.StartsWith("/api/budgets/"))  _budgets.GetById(request, response);
                    else ResponseHelper.NotFound(response, "Budget route not found");
                    return;
                }

                // ── INVOICES (Maxi) ──────────────────────────────────
                if (path.StartsWith("/api/invoices"))
                {
                    if      (method == "GET"  && path == "/api/invoices")           _invoices.GetAll(response);
                    else if (method == "POST" && path == "/api/invoices")           _invoices.Create(request, response);
                    else if (method == "PUT"  && path.StartsWith("/api/invoices/")) _invoices.UpdateStatus(request, response);
                    else if (method == "GET"  && path.StartsWith("/api/invoices/")) _invoices.GetById(request, response);
                    else ResponseHelper.NotFound(response, "Invoice route not found");
                    return;
                }

                // ── SERVICES (Oscar) ─────────────────────────────────
                if (path.StartsWith("/api/services"))
                {
                    if      (method == "GET"  && path == "/api/services")           _services.GetAll(response);
                    else if (method == "POST" && path == "/api/services")           _services.Create(request, response);
                    else if (method == "GET"  && path.StartsWith("/api/services/")) _services.GetById(request, response);
                    else ResponseHelper.NotFound(response, "Service route not found");
                    return;
                }

                // ── 404 ──────────────────────────────────────────────
                ResponseHelper.NotFound(response, $"Route not found: {method} {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                ResponseHelper.InternalError(response);
            }
        }

        private static bool TryGetId(string path, string prefix, out int id)
        {
            id = 0;
            if (!path.StartsWith(prefix)) return false;
            var segment = path.Substring(prefix.Length).Trim('/');
            return int.TryParse(segment, out id);
        }
    }
} */