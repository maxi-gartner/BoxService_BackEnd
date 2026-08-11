using System.Net;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd.Router
{
    public class ServiceRouter
    {
        private readonly ServicesController _controller = new();

        public void Route(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var method = request.HttpMethod.ToUpper();
            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";

            if (method == "GET" && path == "/api/services")
            {
                _controller.GetAll(response);
                return;
            }

            if (method == "POST" && path == "/api/services")
            {
                _controller.Create(request, response);
                return;
            }

            // Crear detalle para un service
            // POST /api/services/{id}/details
            if (method == "POST" &&
                path.StartsWith("/api/services/") &&
                path.EndsWith("/details"))
            {
                _controller.CreateDetail(request, response);
                return;
            }

            // Traer el detalle de un service (para el ticket imprimible).
            // GET /api/services/{id}/details
            if (method == "GET" &&
                path.StartsWith("/api/services/") &&
                path.EndsWith("/details") &&
                TryGetDetailsServiceId(path, out int detailsServiceId))
            {
                _controller.GetDetails(response, detailsServiceId);
                return;
            }

            if (method == "GET" && path.StartsWith("/api/services/"))
            {
                _controller.GetById(request, response);
                return;
            }

            ResponseHelper.NotFound(response, "Service route not found");
        }

        private static bool TryGetDetailsServiceId(string path, out int id)
        {
            id = 0;

            const string prefix = "/api/services/";
            const string suffix = "/details";

            if (!path.StartsWith(prefix) || !path.EndsWith(suffix))
                return false;

            var segment = path.Substring(prefix.Length);
            segment = segment.Substring(0, segment.Length - suffix.Length).Trim('/');

            return int.TryParse(segment, out id);
        }
    }
}