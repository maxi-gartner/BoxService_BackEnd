using System.Net;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd.Router
{
    public class ServiceRouter
    {
        private readonly ServicesController _controller = new();

        public void Route(HttpListenerContext context)
        {
            var request  = context.Request;
            var response = context.Response;

            var method = request.HttpMethod.ToUpper();
            var path   = request.Url?.AbsolutePath.TrimEnd('/') ?? "";

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

            if (method == "GET" && path.StartsWith("/api/services/"))
            {
                _controller.GetById(request, response);
                return;
            }

            ResponseHelper.NotFound(response, "Service route not found");
        }
    }
}