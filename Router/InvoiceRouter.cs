using System.Net;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd.Router
{
    public class InvoiceRouter
    {
        private readonly InvoiceController _controller = new();

        public void Route(HttpListenerContext context)
        {
            var request  = context.Request;
            var response = context.Response;

            var method = request.HttpMethod.ToUpper();
            var path   = request.Url?.AbsolutePath.TrimEnd('/') ?? "";
            var basePath = path.StartsWith("/api/facturas") ? "/api/facturas" : "/api/invoices";

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

            // PATCH /api/invoices/{id} — cambia el estado (paid | cancelled).
            if (method == "PATCH" && path.StartsWith(basePath + "/"))
            {
                _controller.UpdateStatus(request, response);
                return;
            }

            if (method == "GET" && path.StartsWith(basePath + "/"))
            {
                _controller.GetById(request, response);
                return;
            }

            ResponseHelper.NotFound(response, "Invoice route not found");
        }
    }
}
