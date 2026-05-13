using System.Net;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd.Router
{
    public class BudgetRouter
    {
        private readonly BudgetController _controller = new();

        public void Route(HttpListenerContext context)
        {
            var request  = context.Request;
            var response = context.Response;

            var method = request.HttpMethod.ToUpper();
            var path   = request.Url?.AbsolutePath.TrimEnd('/') ?? "";

            if (method == "GET" && path == "/api/budgets")
            {
                _controller.GetAll(response);
                return;
            }

            if (method == "POST" && path == "/api/budgets")
            {
                _controller.Create(request, response);
                return;
            }

            if (method == "PUT" && path.Contains("/status"))
            {
                _controller.UpdateStatus(request, response);
                return;
            }

            if (method == "POST" && path.Contains("/approve"))
            {
                _controller.Approve(request, response);
                return;
            }

            if (method == "GET" && path.StartsWith("/api/budgets/"))
            {
                _controller.GetById(request, response);
                return;
            }

            ResponseHelper.NotFound(response, "Budget route not found");
        }
    }
}