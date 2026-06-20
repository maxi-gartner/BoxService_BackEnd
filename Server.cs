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
        private readonly HttpListener _listener;
        private readonly IndexRouter _router;

        private const string Prefix = "http://localhost:5001/";

        public Server()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(Prefix);

            var clientRepository = new ClientRepository();
            var clientService    = new ClientService(clientRepository);
            var clientController = new ClientController(clientService);
            var clientRouter     = new ClientRouter(clientController);

            // ── VEHICLES ─────────────────────────────

            var vehicleRepository = new VehicleRepository();
            var vehicleService    = new VehicleService(vehicleRepository);
            var vehicleController = new VehicleController(vehicleService);
            var vehicleRouter     = new VehicleRouter(vehicleController);

            // ── OTHER ROUTERS ───────────────────────

            var budgetRouter  = new BudgetRouter();
            var invoiceRouter = new InvoiceRouter();
            var serviceRouter = new ServiceRouter();
            var healthRouter  = new HealthRouter();

            // ── MAIN ROUTER ─────────────────────────

            _router = new IndexRouter(
                clientRouter,
                vehicleRouter,
                budgetRouter,
                invoiceRouter,
                serviceRouter,
                healthRouter
            );
        }

        public void Start()
        {
            _listener.Start();

            Console.WriteLine($"BoxService corriendo en {Prefix}");
            Console.WriteLine("Presioná Ctrl+C para detener...\n");

            while (_listener.IsListening)
            {
                try
                {
                    var context = _listener.GetContext();

                    Task.Run(() => HandleRequest(context));
                }
                catch (HttpListenerException ex)
                {
                    Console.WriteLine($"Listener detenido: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request  = context.Request;
            var response = context.Response;

            try
            {
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                Console.WriteLine($"[{request.HttpMethod}] {request.Url?.AbsolutePath}");

                _router.Route(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex}");

                try
                {
                    ResponseHelper.InternalError(response);
                }
                catch
                {
                }
            }
        }

        public void Stop()
        {
            _listener.Stop();
            _listener.Close();
        }
    }
}
