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

            // GET /api/vehiculos            -> lista completa
            // GET /api/vehiculos?plate=ABC   -> filtra por patente (antes era /vehiculos/buscar)
            if (method == "GET" && path == "/api/vehiculos")
            {
                _controller.GetAll(request, response);
                return;
            }

            if (method == "GET" && TryGetHistoryId(path, out int historyVehicleId))
            {
                _controller.GetHistory(response, historyVehicleId);
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

        private static bool TryGetHistoryId(string path, out int id)
        {
            id = 0;

            const string prefix = "/api/vehiculos/";
            const string suffix = "/historial";

            if (!path.StartsWith(prefix) || !path.EndsWith(suffix))
                return false;

            var segment = path.Substring(prefix.Length);
            segment = segment.Substring(0, segment.Length - suffix.Length).Trim('/');

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
