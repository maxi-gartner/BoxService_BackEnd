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

            if (method == "GET" && path == "/api/invoices")
            {
                _controller.GetAll(response);
                return;
            }

            if (method == "POST" && path == "/api/invoices")
            {
                _controller.Create(request, response);
                return;
            }

            if (method == "PUT" && path.StartsWith("/api/invoices/"))
            {
                _controller.UpdateStatus(request, response);
                return;
            }

            if (method == "GET" && path.StartsWith("/api/invoices/"))
            {
                _controller.GetById(request, response);
                return;
            }

            ResponseHelper.NotFound(response, "Invoice route not found");
        }
    }
}