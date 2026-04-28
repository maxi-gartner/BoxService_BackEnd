using System;
using System.Net;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd
{
    public class Router
    {
        private readonly HealthController       _health;
        private readonly PresupuestosController _presupuestos;
        private readonly FacturasController     _facturas;

        // Descomentar cuando cada integrante conecte su controller
        // private readonly ClientesController    _clientes;
        // private readonly VehiculosController   _vehiculos;
         private readonly ServicesController    _services; // Descomentado por Oscar

        public Router()
        {
            _health       = new HealthController();
            _presupuestos = new PresupuestosController();
            _facturas     = new FacturasController();
            _services = new ServicesController(); // Agregado por Oscar
        }

        public void Route(HttpListenerRequest request, HttpListenerResponse response)
        {
            var method = request.HttpMethod.ToUpper();
            var path   = request.Url?.AbsolutePath.TrimEnd('/') ?? "/";

            try
            {
                // ── Health ──────────────────────────────────────────────
                if (method == "GET" && path == "/health")
                {
                    _health.GetHealth(response);
                    return;
                }

                // ── Presupuestos ────────────────────────────────────────
                if (method == "GET"  && path == "/api/presupuestos")                      { _presupuestos.GetAll(response);              return; }
                if (method == "GET"  && path.StartsWith("/api/presupuestos/"))             { _presupuestos.GetById(request, response);    return; }
                if (method == "POST" && path == "/api/presupuestos")                      { _presupuestos.Create(request, response);     return; }
                if (method == "PUT"  && path.Contains("/estado"))                         { _presupuestos.CambiarEstado(request, response); return; }
                if (method == "POST" && path.Contains("/aprobar"))                        { _presupuestos.Aprobar(request, response);    return; }

                // ── Facturas ────────────────────────────────────────────
                if (method == "GET"  && path == "/api/facturas")                          { _facturas.GetAll(response);                  return; }
                if (method == "GET"  && path.StartsWith("/api/facturas/"))                { _facturas.GetById(request, response);        return; }
                if (method == "POST" && path == "/api/facturas")                          { _facturas.Create(request, response);         return; }
                if (method == "PUT"  && path.StartsWith("/api/facturas/"))                { _facturas.CambiarEstado(request, response);  return; }

                // ── Clientes (Cristhian — descomentar al conectar) ──────
                // if (method == "GET"  && path == "/api/clientes")                       { _clientes.GetAll(response);            return; }
                // if (method == "GET"  && path.StartsWith("/api/clientes/"))             { _clientes.GetById(request, response);  return; }
                // if (method == "POST" && path == "/api/clientes")                       { _clientes.Create(request, response);   return; }

                // ── Vehículos (Leo — descomentar al conectar) ───────────
                // if (method == "GET"  && path == "/api/vehiculos")                      { _vehiculos.GetAll(response);            return; }
                // if (method == "GET"  && path.StartsWith("/api/vehiculos/"))            { _vehiculos.GetById(request, response);  return; }
                // if (method == "POST" && path == "/api/vehiculos")                      { _vehiculos.Create(request, response);   return; }

                // ── Services (Oscar — descomentar al conectar) ──────────
                if (method == "GET"  && path == "/api/services")                       { _services.GetAll(response);            return; } // Descomentado por Oscar
                if (method == "GET"  && path.StartsWith("/api/services/"))             { _services.GetById(request, response);  return; } // Descomentado por Oscar
                if (method == "POST" && path == "/api/services")                       { _services.Create(request, response);   return; } // Descomentado por Oscar

                // ── 404 ─────────────────────────────────────────────────
                ResponseHelper.Send(response, 404, new
                {
                    success = false,
                    data    = (object?)null,
                    error   = new { code = 404, message = $"Ruta no encontrada: {method} {path}" }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                ResponseHelper.InternalError(response);
            }
        }
    }
}
