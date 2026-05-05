using System;
using System.Net;
using System.Threading.Tasks;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd
{
    public class Router
    {
        private readonly HealthController _health;
        private readonly PresupuestosController _presupuestos;
        private readonly FacturasController _facturas;
        private readonly VehicleController _vehiculos;
        private readonly ServicesController _services;

        public Router()
        {
            _health       = new HealthController();
            _presupuestos = new PresupuestosController();
            _facturas     = new FacturasController();
            _vehiculos    = new VehicleController();
            _services     = new ServicesController();
        }

        public async Task RouteAsync(HttpListenerContext context)
        {
            var request  = context.Request;
            var response = context.Response;

            var method = request.HttpMethod.ToUpper();
            var path   = request.Url?.AbsolutePath.TrimEnd('/') ?? "/";

            try
            {
                // ── ROOT ──────────────────────────────────────────────
                if (method == "GET" && path == "/")
                {
                    ResponseHelper.Ok(response, new
                    {
                        message = "BoxService Backend",
                        status  = "online",
                        version = "1.0.0"
                    });
                    return;
                }

                // ── HEALTH ────────────────────────────────────────────
                if (method == "GET" && path == "/health")
                {
                    _health.GetHealth(response);
                    return;
                }

                // ── VEHICULOS ─────────────────────────────────────────
                if (path.StartsWith("/api/vehiculos"))
                {
                    if (method == "GET" && path == "/api/vehiculos")
                        await _vehiculos.GetAllAsync(context);

                    else if (method == "GET" && path.Contains("/buscar"))
                        await _vehiculos.SearchByPlateAsync(context);

                    else if (method == "GET" && path.EndsWith("/historial"))
                        await _vehiculos.GetVehicleHistoryAsync(context);

                    else if (method == "POST" && path == "/api/vehiculos")
                        await _vehiculos.CreateAsync(context);

                    else if (TryGetId(path, "/api/vehiculos/", out int id))
                        await _vehiculos.GetByIdAsync(context, id);

                    else
                        ResponseHelper.NotFound(response, "Ruta de vehículos no válida");

                    return;
                }

                // ── PRESUPUESTOS ──────────────────────────────────────
                if (path.StartsWith("/api/presupuestos"))
                {
                    if (method == "GET" && path == "/api/presupuestos")
                        _presupuestos.GetAll(response);

                    else if (method == "POST")
                        _presupuestos.Create(request, response);

                    else
                        _presupuestos.GetById(request, response);

                    return;
                }

                // ── FACTURAS ──────────────────────────────────────────
                if (path.StartsWith("/api/facturas"))
                {
                    if (method == "GET" && path == "/api/facturas")
                        _facturas.GetAll(response);

                    else if (method == "POST")
                        _facturas.Create(request, response);

                    else
                        _facturas.GetById(request, response);

                    return;
                }

                // ── SERVICES ──────────────────────────────────────────
                if (path.StartsWith("/api/services"))
                {
                    if (method == "GET" && path == "/api/services")
                        _services.GetAll(response);

                    else if (method == "POST")
                        _services.Create(request, response);

                    else
                        _services.GetById(request, response);

                    return;
                }

                // ── 404 ───────────────────────────────────────────────
                ResponseHelper.NotFound(response, $"Ruta no encontrada: {method} {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                ResponseHelper.InternalError(response);
            }
        }

        private bool TryGetId(string path, string prefix, out int id)
        {
            id = 0;

            if (!path.StartsWith(prefix))
                return false;

            var segment = path.Substring(prefix.Length).Trim('/');

            return int.TryParse(segment, out id);
        }
    }
}