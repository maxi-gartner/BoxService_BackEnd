using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;

namespace BoxService_BackEnd.Controllers
{
    public class CatalogController
    {
        private readonly CatalogService _service = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public void GetAll(HttpListenerResponse response)
        {
            ResponseHelper.Ok(response, _service.GetAll());
        }

        public void Create(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = ReadBody(request);

            CatalogItemCreateRequest? req;
            try
            {
                req = JsonSerializer.Deserialize<CatalogItemCreateRequest>(body, JsonOptions);
            }
            catch
            {
                ResponseHelper.BadRequest(response, "Invalid JSON body.");
                return;
            }

            if (req == null) { ResponseHelper.BadRequest(response, "Invalid JSON body."); return; }

            try
            {
                var created = _service.Create(req);
                ResponseHelper.Created(response, created);
            }
            catch (System.ArgumentException ex)
            {
                ResponseHelper.BadRequest(response, ex.Message);
            }
        }

        public void Update(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            var body = ReadBody(request);

            CatalogItemUpdateRequest? req;
            try
            {
                req = JsonSerializer.Deserialize<CatalogItemUpdateRequest>(body, JsonOptions);
            }
            catch
            {
                ResponseHelper.BadRequest(response, "Invalid JSON body.");
                return;
            }

            if (req == null) { ResponseHelper.BadRequest(response, "Invalid JSON body."); return; }

            var (ok, notFound, error) = _service.Update(id, req);

            if (!ok)
            {
                if (notFound) ResponseHelper.NotFound(response, error);
                else ResponseHelper.BadRequest(response, error);
                return;
            }

            ResponseHelper.Ok(response, new { message = "Catalog item updated" });
        }

        public void Delete(HttpListenerResponse response, int id)
        {
            var deleted = _service.Delete(id);

            if (!deleted)
            {
                ResponseHelper.NotFound(response, "Catalog item not found");
                return;
            }

            ResponseHelper.Ok(response, new { message = "Catalog item deleted" });
        }

        private static string ReadBody(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
