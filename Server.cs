using System;
using System.Net;

namespace BoxService_BackEnd
{
    public class Server
    {
        private readonly HttpListener _listener;
        private readonly Router _router;
        private const string Prefix = "http://localhost:5000/";

        public Server()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(Prefix);
            _router = new Router();
        }

        public void Start()
        {
            _listener.Start();
            Console.WriteLine($"BoxService corriendo en {Prefix}");
            Console.WriteLine("Presiona Ctrl+C para detener...\n");

            while (true)
            {
                try
                {
                    var context = _listener.GetContext();
                    HandleRequest(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] {ex.Message}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request  = context.Request;
            var response = context.Response;

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
            _router.Route(request, response);
        }

        public void Stop()
        {
            _listener.Stop();
            _listener.Close();
        }
    }
}
