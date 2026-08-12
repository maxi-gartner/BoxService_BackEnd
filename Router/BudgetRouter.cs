using System.Net;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd.Router
{
    public class BudgetRouter
    {
        private readonly BudgetController _controller = new();

        public void Route(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var method = request.HttpMethod.ToUpper();
            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";
            var basePath = path.StartsWith("/api/presupuestos") ? "/api/presupuestos" : "/api/budgets";

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

            // Vincula un presupuesto con el service creado desde el módulo de Services.
            // PUT /api/budgets/{id}/service
            if (method == "PUT" &&
                path.StartsWith(basePath + "/") &&
                path.EndsWith("/service"))
            {
                _controller.AssignService(request, response);
                return;
            }

            // Transición de estado (sent | rejected | approved) unificada en un solo
            // endpoint orientado al recurso, en vez de /status y /approve separados.
            // PATCH /api/budgets/{id}
            if (method == "PATCH" && TryGetId(path, basePath + "/", out _))
            {
                _controller.UpdateStatus(request, response);
                return;
            }

            if (method == "GET" && path.StartsWith(basePath + "/"))
            {
                _controller.GetById(request, response);
                return;
            }

            ResponseHelper.NotFound(response, "Budget route not found");
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
