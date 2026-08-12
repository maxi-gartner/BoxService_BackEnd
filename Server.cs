using System;
using System.Net;
using System.Threading.Tasks;

using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Services;
using BoxService_BackEnd.Controllers;
using BoxService_BackEnd.Router;

namespace BoxService_BackEnd
{
    public class Server
    {
        // HttpListener es el objeto que permite que el backend escuche peticiones HTTP.
        // Es como decir: "quedate escuchando si alguien llama a localhost:5001".
        private readonly HttpListener _listener;

        // Router principal.
        // Todas las peticiones que llegan al servidor se mandan primero a IndexRouter.
        private readonly IndexRouter _router;

        // Dirección donde va a correr el backend.
        // El frontend llama a esta URL desde api.js.
        private const string Prefix = "http://localhost:5001/";

        // API key simple compartida con el frontend (header X-Api-Key).
        // No es un sistema de auth completo — es un candado mínimo para que
        // la API no quede totalmente abierta a cualquiera.
        private readonly string _apiKey;

        public Server(string apiKey)
        {
            _apiKey = apiKey;

            // Se crea el listener HTTP.
            _listener = new HttpListener();

            // Se le dice al listener en qué URL tiene que escuchar.
            // En este caso: http://localhost:5001/
            _listener.Prefixes.Add(Prefix);

            // ── CLIENTS ──────────────────────────────
            // Acá se arma la cadena del módulo clientes:
            // Repository → Service → Controller → Router

            var clientRepository = new ClientRepository();
            var clientService = new ClientService(clientRepository);
            var clientController = new ClientController(clientService);
            var clientRouter = new ClientRouter(clientController);

            // ── VEHICLES ─────────────────────────────
            // Acá se arma la cadena del módulo vehículos:
            // Repository → Service → Controller → Router
            // GetHistory necesita ServicesService para traer el historial real
            // de services de un vehículo.

            var vehicleRepository = new VehicleRepository();
            var vehicleService = new VehicleService(vehicleRepository);
            var vehicleController = new VehicleController(vehicleService, new ServicesService());
            var vehicleRouter = new VehicleRouter(vehicleController);

            // ── OTHER ROUTERS ───────────────────────
            // Estos routers se crean directo porque internamente ya crean
            // sus controllers/services/repositories, o están armados distinto.

            var budgetRouter = new BudgetRouter();
            var invoiceRouter = new InvoiceRouter();
            var serviceRouter = new ServiceRouter();
            var catalogRouter = new CatalogRouter();
            var healthRouter = new HealthRouter();

            // ── MAIN ROUTER ─────────────────────────
            // Se crea el router principal y se le pasan todos los routers específicos.
            // IndexRouter después decide a cuál mandar cada petición según la URL.

            _router = new IndexRouter(
                clientRouter,
                vehicleRouter,
                budgetRouter,
                invoiceRouter,
                serviceRouter,
                catalogRouter,
                healthRouter
            );
        }

        public void Start()
        {
            // Arranca el servidor.
            // Desde este momento el backend empieza a escuchar peticiones.
            _listener.Start();

            Console.WriteLine($"BoxService corriendo en {Prefix}");
            Console.WriteLine("Presioná Ctrl+C para detener...\n");

            // Mientras el servidor esté escuchando, queda esperando peticiones.
            while (_listener.IsListening)
            {
                try
                {
                    // GetContext() se queda esperando hasta que llegue una petición.
                    // Cuando el frontend llama al backend, acá se recibe esa petición.
                    var context = _listener.GetContext();

                    // Se procesa la petición en una tarea aparte.
                    // Esto permite que el servidor pueda seguir recibiendo otras peticiones.
                    Task.Run(() => HandleRequest(context));
                }
                catch (HttpListenerException ex)
                {
                    // Si el listener se detiene, sale del while.
                    Console.WriteLine($"Listener detenido: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    // Cualquier otro error general.
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            // request = lo que llegó desde el frontend.
            // Tiene método, URL, headers, body, etc.
            var request = context.Request;

            // response = lo que el backend va a devolver.
            var response = context.Response;

            try
            {
                // ── CORS ─────────────────────────────
                // Esto permite que el frontend, que corre en otro puerto
                // por ejemplo 127.0.0.1:5500, pueda llamar al backend
                // que corre en localhost:5001.

                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, PATCH, DELETE, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Api-Key");

                // El navegador a veces manda una petición OPTIONS antes de una real.
                // Es como una pregunta previa:
                // "¿Puedo llamar a este backend con estos métodos y headers?"
                if (request.HttpMethod == "OPTIONS")
                {
                    // 204 significa: OK, pero sin contenido.
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                // Muestra en consola qué método y URL llegaron.
                // Ejemplo:
                // [GET] /api/services
                // [POST] /api/services
                Console.WriteLine($"[{request.HttpMethod}] {request.Url?.AbsolutePath}");

                // ── API KEY ──────────────────────────
                // Todo lo que no sea la raíz o /health necesita el header X-Api-Key.
                var path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";
                var isPublicRoute = path == "" || path.StartsWith("/health");

                if (!isPublicRoute)
                {
                    var providedKey = request.Headers["X-Api-Key"];

                    if (string.IsNullOrEmpty(providedKey) || providedKey != _apiKey)
                    {
                        ResponseHelper.Unauthorized(response, "Missing or invalid API key.");
                        return;
                    }
                }

                // Manda la petición al router principal.
                // A partir de acá, IndexRouter decide a qué router específico va.
                _router.Route(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex}");

                try
                {
                    // Si algo explota en el servidor, devuelve error 500.
                    ResponseHelper.InternalError(response);
                }
                catch
                {
                    // Si incluso falla al responder, no hace nada.
                }
            }
        }

        public void Stop()
        {
            // Detiene el servidor.
            _listener.Stop();
            _listener.Close();
        }
    }
}