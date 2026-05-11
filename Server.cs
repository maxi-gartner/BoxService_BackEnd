using System;
using System.Net;
using System.Threading.Tasks;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Services;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd
{
    public class Server
    {
        private readonly HttpListener _listener;
        private readonly Router _router;
        private const string Prefix = "http://localhost:5001/";

        public Server(string connectionString)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(Prefix);

            // Vehículos — inyección de dependencias
            var vehicleRepository  = new VehicleRepository(connectionString);
            var vehicleService     = new VehicleService(vehicleRepository);
            var vehicleController  = new VehicleController(vehicleService);

            _router = new Router(vehicleController);
        }

        public async Task StartAsync()
        {
            _listener.Start();
            Console.WriteLine($"BoxService corriendo en {Prefix}");
            Console.WriteLine("Presioná Ctrl+C para detener...\n");

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
                    Console.WriteLine($"Error aceptando request: {ex.Message}");
                }
            }
        }

        private async Task HandleContextAsync(HttpListenerContext context)
        {
            var request  = context.Request;
            var response = context.Response;

            try
            {
                // CORS
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

                await _router.RouteAsync(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                ResponseHelper.InternalError(response);
            }
            finally
            {
                try { response.OutputStream.Close(); } catch { }
            }
        }

        public void Stop()
        {
            _listener.Stop();
            _listener.Close();
        }
    }
}