using System;
using System.Net;
using System.Threading.Tasks;

namespace BoxService_BackEnd
{
    public class Server
    {
        private readonly HttpListener _listener;
        private readonly Router _router;
        private const string Prefix = "http://localhost:5000/";

        public Server(string connectionString)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(Prefix);

            // Inyección de dependencias (tu parte)
            var repository = new VehicleRepository(connectionString);
            var service    = new VehicleService(repository);
            var controller = new VehicleController(service);

            _router = new Router(controller);
        }

        public async Task StartAsync()
        {
            _listener.Start();
            Console.WriteLine($"🚀 BoxService corriendo en {Prefix}");

            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleContextAsync(context));
                }
                catch (HttpListenerException ex)
                {
                    Console.WriteLine($"Listener detenido: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error aceptando request: {ex}");
                }
            }
        }

        private async Task HandleContextAsync(HttpListenerContext context)
        {
            var request  = context.Request;
            var response = context.Response;

            try
            {
                // ── CORS ─────────────────────────────────────────────
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                // ── LOG ──────────────────────────────────────────────
                Console.WriteLine($"[{request.HttpMethod}] {request.Url?.AbsolutePath}");

                // ── ROUTING ─────────────────────────────────────────
                await _router.RouteAsync(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex}");
                ResponseHelper.InternalError(response);
            }
            finally
            {
                response.OutputStream.Close();
            }
        }

        public void Stop()
        {
            _listener.Stop();
            _listener.Close();
        }
    }
}