using System;
using System.IO;
using System.Net;
using System.Text.Json;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;

namespace BoxService_BackEnd.Controllers
{
    public class BudgetController
    {
        private readonly BudgetService _service = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

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
            var body = ReadBody(request);

            BudgetCreateRequest? req;
            try
            {
                req = JsonSerializer.Deserialize<BudgetCreateRequest>(body, JsonOptions);
            }
            catch
            {
                ResponseHelper.BadRequest(response, "Invalid JSON body.");
                return;
            }

            if (req == null)
            {
                ResponseHelper.BadRequest(response, "Invalid JSON body.");
                return;
            }

            var (ok, _, error, result) = _service.Create(req);

            if (!ok)
            {
                ResponseHelper.BadRequest(response, error);
                return;
            }

            ResponseHelper.Created(response, result!);
        }

        // Reemplaza a los antiguos PUT /{id}/status y POST /{id}/approve.
        // Un solo endpoint orientado al recurso: PATCH /api/budgets/{id}.
        public void UpdateStatus(HttpListenerRequest request, HttpListenerResponse response)
        {
            var id = ParseId(request.Url?.AbsolutePath, 3);

            if (id == null)
            {
                ResponseHelper.BadRequest(response, "Invalid ID");
                return;
            }

            var body = ReadBody(request);

            BudgetStatusRequest? req;
            try
            {
                req = JsonSerializer.Deserialize<BudgetStatusRequest>(body, JsonOptions);
            }
            catch
            {
                ResponseHelper.BadRequest(response, "Invalid JSON body.");
                return;
            }

            if (req == null)
            {
                ResponseHelper.BadRequest(response, "Invalid JSON body.");
                return;
            }

            var (ok, notFound, error, result) = _service.UpdateStatus(id.Value, req);

            if (!ok)
            {
                if (notFound) ResponseHelper.NotFound(response, error);
                else ResponseHelper.BadRequest(response, error);
                return;
            }

            ResponseHelper.Ok(response, result!);
        }

        // Recibe el id_service creado desde Services y lo guarda en presupuestos.id_service.
        // PUT /api/budgets/{budgetId}/service
        public void AssignService(HttpListenerRequest request, HttpListenerResponse response)
        {
            var id = ParseId(request.Url?.AbsolutePath, 3);

            if (id == null)
            {
                ResponseHelper.BadRequest(response, "Invalid budget ID");
                return;
            }

            var body = ReadBody(request);

            AssignServiceRequest? data;
            try
            {
                data = JsonSerializer.Deserialize<AssignServiceRequest>(body, JsonOptions);
            }
            catch
            {
                ResponseHelper.BadRequest(response, "Invalid JSON body.");
                return;
            }

            if (data == null || data.ServiceId <= 0)
            {
                ResponseHelper.BadRequest(response, "Invalid service ID");
                return;
            }

            var (ok, notFound, error) = _service.AssignService(id.Value, data.ServiceId);

            if (!ok)
            {
                if (notFound) ResponseHelper.NotFound(response, error);
                else ResponseHelper.BadRequest(response, error);
                return;
            }

            ResponseHelper.Ok(response, new { message = "Budget linked to service" });
        }

        private static string ReadBody(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? System.Text.Encoding.UTF8);
            return reader.ReadToEnd();
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
    }
}
