using System.IO;
using System.Net;
using System.Text.Json;
using BoxService_BackEnd.Services;

namespace BoxService_BackEnd.Controllers
{
    public class BudgetController
    {
        private readonly BudgetService _service = new();

        public void GetAll(HttpListenerResponse response)
        {
            var list = _service.GetAll();
            ResponseHelper.Ok(response, list);
        }

        public void GetById(HttpListenerRequest request, HttpListenerResponse response)
        {
            var id = ParseId(request.Url?.AbsolutePath, 3);

            if (id == null)
            {
                ResponseHelper.BadRequest(response, "Invalid ID");
                return;
            }

            var result = _service.GetById(id.Value);

            if (result == null)
            {
                ResponseHelper.NotFound(response, "Budget not found");
                return;
            }

            ResponseHelper.Ok(response, result);
        }

        public void Create(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = new StreamReader(request.InputStream).ReadToEnd();

            var (ok, error, result) = _service.Create(body);

            if (!ok)
            {
                ResponseHelper.BadRequest(response, error);
                return;
            }

            ResponseHelper.Created(response, result!);
        }

        public void UpdateStatus(HttpListenerRequest request, HttpListenerResponse response)
        {
            var id = ParseId(request.Url?.AbsolutePath, 3);

            if (id == null)
            {
                ResponseHelper.BadRequest(response, "Invalid ID");
                return;
            }

            var body = new StreamReader(request.InputStream).ReadToEnd();

            var (ok, error) = _service.UpdateStatus(id.Value, body);

            if (!ok)
            {
                ResponseHelper.BadRequest(response, error);
                return;
            }

            ResponseHelper.Ok(response, new { message = "Status updated" });
        }

        public void Approve(HttpListenerRequest request, HttpListenerResponse response)
        {
            var id = ParseId(request.Url?.AbsolutePath, 3);

            if (id == null)
            {
                ResponseHelper.BadRequest(response, "Invalid ID");
                return;
            }

            var (ok, error, result) = _service.Approve(id.Value);

            if (!ok)
            {
                ResponseHelper.BadRequest(response, error);
                return;
            }

            // CAMBIO:
            // Ahora aprobar un presupuesto NO crea un service.
            // Devuelve el presupuesto aprobado o el id del presupuesto, según lo maneje BudgetService.
            ResponseHelper.Created(response, result!);
        }

        // NUEVO:
        // Recibe el id_service creado desde Services y lo guarda en presupuestos.id_service.
        // Endpoint esperado:
        // PUT /api/budgets/{budgetId}/service
        public void AssignService(HttpListenerRequest request, HttpListenerResponse response)
        {
            var id = ParseId(request.Url?.AbsolutePath, 3);

            if (id == null)
            {
                ResponseHelper.BadRequest(response, "Invalid budget ID");
                return;
            }

            var body = new StreamReader(request.InputStream).ReadToEnd();

            var data = JsonSerializer.Deserialize<AssignServiceRequest>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (data == null || data.ServiceId <= 0)
            {
                ResponseHelper.BadRequest(response, "Invalid service ID");
                return;
            }

            var (ok, error) = _service.AssignService(id.Value, data.ServiceId);

            if (!ok)
            {
                ResponseHelper.BadRequest(response, error);
                return;
            }

            ResponseHelper.Ok(response, new { message = "Budget linked to service" });
        }

        private static int? ParseId(string? path, int segment)
        {
            if (path == null) return null;

            var parts = path.Trim('/').Split('/');

            if (parts.Length > segment - 1 &&
                int.TryParse(parts[segment - 1], out var id))
            {
                return id;
            }

            return null;
        }

        // NUEVO:
        // Modelo interno para leer el body del PUT /api/budgets/{id}/service.
        private class AssignServiceRequest
        {
            public int ServiceId { get; set; }
        }
    }
}