using System.Net;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd.Router
{
    public class ClientRouter
    {
        private readonly ClientController _controller;

        public ClientRouter(ClientController controller)
        {
            _controller = controller;
        }

        public void Route(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var method = request.HttpMethod.ToUpper();
            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";
            var basePath = path.StartsWith("/api/clientes") ? "/api/clientes" : "/api/clients";

            if (method == "GET" && path == basePath)
            {
                _controller.GetAll(response);
                return;
            }

            if (method == "POST" && path == basePath)
            {
                _controller.Create(request, response);
                return;
            }

            if (method == "GET" && TryGetVehiclesPath(path, basePath, out int clientId))
            {
                _controller.GetVehicles(response, clientId);
                return;
            }

            if (method == "GET" && TryGetId(path, basePath + "/", out int id))
            {
                _controller.GetById(response, id);
                return;
            }

            ResponseHelper.NotFound(response, "Client route not found.");
        }

        private static bool TryGetVehiclesPath(string path, string basePath, out int id)
        {
            id = 0;

            var prefix = basePath + "/";
            if (!path.StartsWith(prefix) || !path.EndsWith("/vehicles"))
                return false;

            var segment = path.Substring(prefix.Length);
            segment = segment.Substring(0, segment.Length - "/vehicles".Length).Trim('/');

            return int.TryParse(segment, out id);
        }

        private static bool TryGetId(string path, string prefix, out int id)
        {
            id = 0;

            if (!path.StartsWith(prefix))
                return false;

            var segment = path.Substring(prefix.Length).Trim('/');

            return int.TryParse(segment, out id);
        }
    }
}
