using System.IO;
using System.Net;
using System.Text.Json;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;

namespace BoxService_BackEnd.Controllers
{
    public class InvoiceController
    {
        private readonly InvoiceService _service = new();

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
            var id = ParseId(request.Url?.AbsolutePath);
            if (id == null) { ResponseHelper.BadRequest(response, "Invalid ID"); return; }

            var invoice = _service.GetById(id.Value);
            if (invoice == null) { ResponseHelper.NotFound(response, "Invoice not found"); return; }

            ResponseHelper.Ok(response, invoice);
        }

        public void Create(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = ReadBody(request);

            InvoiceCreateRequest? req;
            try
            {
                req = JsonSerializer.Deserialize<InvoiceCreateRequest>(body, JsonOptions);
            }
            catch
            {
                ResponseHelper.BadRequest(response, "Invalid JSON body.");
                return;
            }

            if (req == null) { ResponseHelper.BadRequest(response, "Invalid JSON body."); return; }

            var (ok, _, error, result) = _service.Create(req);
            if (!ok) { ResponseHelper.BadRequest(response, error); return; }

            ResponseHelper.Created(response, result!);
        }

        // PATCH /api/invoices/{id}
        public void UpdateStatus(HttpListenerRequest request, HttpListenerResponse response)
        {
            var id = ParseId(request.Url?.AbsolutePath);
            if (id == null) { ResponseHelper.BadRequest(response, "Invalid ID"); return; }

            var body = ReadBody(request);

            InvoiceStatusRequest? req;
            try
            {
                req = JsonSerializer.Deserialize<InvoiceStatusRequest>(body, JsonOptions);
            }
            catch
            {
                ResponseHelper.BadRequest(response, "Invalid JSON body.");
                return;
            }

            if (req == null) { ResponseHelper.BadRequest(response, "Invalid JSON body."); return; }

            var (ok, notFound, error, result) = _service.UpdateStatus(id.Value, req);

            if (!ok)
            {
                if (notFound) ResponseHelper.NotFound(response, error);
                else ResponseHelper.BadRequest(response, error);
                return;
            }

            ResponseHelper.Ok(response, result!);
        }

        private static string ReadBody(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? System.Text.Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static int? ParseId(string? path)
        {
            if (path == null) return null;
            var parts = path.Trim('/').Split('/');
            if (parts.Length >= 3 && int.TryParse(parts[2], out var id))
                return id;
            return null;
        }
    }
}
