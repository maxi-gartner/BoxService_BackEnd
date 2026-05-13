using System.Net;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd.Router
{
    public class VehicleRouter
    {
        private readonly VehicleController _controller;

        public VehicleRouter(VehicleController controller)
        {
            _controller = controller;
        }

        public void Route(HttpListenerContext context)
        {
            var request  = context.Request;
            var response = context.Response;

            var method = request.HttpMethod.ToUpper();
            var path   = request.Url?.AbsolutePath.TrimEnd('/') ?? "";

            if (method == "GET" && path == "/api/vehiculos")
            {
                _controller.GetAll(response);
                return;
            }

            if (method == "GET" && path.Contains("/buscar"))
            {
                _controller.SearchByPlate(request, response);
                return;
            }

            if (method == "GET" && path.EndsWith("/historial"))
            {
                _controller.GetHistory(response);
                return;
            }

            if (method == "POST" && path == "/api/vehiculos")
            {
                _controller.Create(request, response);
                return;
            }

            if (TryGetId(path, "/api/vehiculos/", out int id))
            {
                _controller.GetById(request, response, id);
                return;
            }

            ResponseHelper.NotFound(response, "Vehicle route not found");
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