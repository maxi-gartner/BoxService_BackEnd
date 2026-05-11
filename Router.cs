using System;
using System.Net;
using System.Threading.Tasks;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd
{
    public class Router
    {
        private readonly HealthController       _health;
        private readonly PresupuestosController _presupuestos;
        private readonly FacturasController     _facturas;
        private readonly VehicleController      _vehiculos;
        private readonly ServicesController     _services;

        // Cristhian descomenta cuando conecte su controller:
        // private readonly ClientesController  _clientes;

        public Router(VehicleController vehicleController)
        {
            _health       = new HealthController();
            _presupuestos = new PresupuestosController();
            _facturas     = new FacturasController();
            _vehiculos    = vehicleController;
            _services     = new ServicesController();

            // Cristhian:
            // _clientes = new ClientesController();
        }

        public async Task RouteAsync(HttpListenerContext context)
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

                // ── CLIENTES (Cristhian — descomentar al conectar) ───
                // if (path.StartsWith("/api/clientes"))
                // {
                //     if (method == "GET" && path == "/api/clientes")           { _clientes.GetAll(response);           return; }
                //     if (method == "GET" && path.Contains("/vehiculos"))        { _clientes.GetVehiculos(request, response); return; }
                //     if (method == "GET" && path.StartsWith("/api/clientes/"))  { _clientes.GetById(request, response);  return; }
                //     if (method == "POST" && path == "/api/clientes")           { _clientes.Create(request, response);   return; }
                //     ResponseHelper.NotFound(response, "Ruta de clientes no válida");
                //     return;
                // }

                // ── VEHÍCULOS (Leo) ──────────────────────────────────
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

                // ── PRESUPUESTOS (Maxi) ──────────────────────────────
                if (path.StartsWith("/api/presupuestos"))
                {
                    if (method == "GET" && path == "/api/presupuestos")           { _presupuestos.GetAll(response);              return; }
                    if (method == "GET" && path.StartsWith("/api/presupuestos/")) { _presupuestos.GetById(request, response);    return; }
                    if (method == "POST" && path == "/api/presupuestos")          { _presupuestos.Create(request, response);     return; }
                    if (method == "PUT"  && path.Contains("/estado"))             { _presupuestos.CambiarEstado(request, response); return; }
                    if (method == "POST" && path.Contains("/aprobar"))            { _presupuestos.Aprobar(request, response);    return; }
                    ResponseHelper.NotFound(response, "Ruta de presupuestos no válida");
                    return;
                }

                // ── FACTURAS (Maxi) ──────────────────────────────────
                if (path.StartsWith("/api/facturas"))
                {
                    if (method == "GET"  && path == "/api/facturas")              { _facturas.GetAll(response);                  return; }
                    if (method == "GET"  && path.StartsWith("/api/facturas/"))    { _facturas.GetById(request, response);        return; }
                    if (method == "POST" && path == "/api/facturas")              { _facturas.Create(request, response);         return; }
                    if (method == "PUT"  && path.StartsWith("/api/facturas/"))    { _facturas.CambiarEstado(request, response);  return; }
                    ResponseHelper.NotFound(response, "Ruta de facturas no válida");
                    return;
                }

                // ── SERVICES (Oscar) ─────────────────────────────────
                if (path.StartsWith("/api/services"))
                {
                    if (method == "GET"  && path == "/api/services")              { _services.GetAll(response);           return; }
                    if (method == "POST" && path == "/api/services")              { _services.Create(request, response);  return; }
                    if (method == "GET"  && path.StartsWith("/api/services/"))    { _services.GetById(request, response); return; }
                    ResponseHelper.NotFound(response, "Ruta de services no válida");
                    return;
                }

                // ── 404 ──────────────────────────────────────────────
                ResponseHelper.NotFound(response, $"Ruta no encontrada: {method} {path}");
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
}