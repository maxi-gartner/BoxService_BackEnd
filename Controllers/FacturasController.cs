using System.IO;
using System.Net;
using BoxService_BackEnd.Services;

namespace BoxService_BackEnd.Controllers
{
    public class FacturasController
    {
        private readonly FacturasService _service = new();

        public void GetAll(HttpListenerResponse response)
        {
            var lista = _service.GetAll();
            ResponseHelper.Ok(response, lista);
        }

        public void GetById(HttpListenerRequest request, HttpListenerResponse response)
        {
            var id = ParseId(request.Url?.AbsolutePath);
            if (id == null) { ResponseHelper.BadRequest(response, "ID inválido"); return; }

            var factura = _service.GetById(id.Value);
            if (factura == null) { ResponseHelper.NotFound(response, "Factura no encontrada"); return; }

            ResponseHelper.Ok(response, factura);
        }

        public void Create(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = new StreamReader(request.InputStream).ReadToEnd();
            var (ok, error, resultado) = _service.Create(body);
            if (!ok) { ResponseHelper.BadRequest(response, error); return; }
            ResponseHelper.Created(response, resultado!);
        }

        public void CambiarEstado(HttpListenerRequest request, HttpListenerResponse response)
        {
            var id = ParseId(request.Url?.AbsolutePath);
            if (id == null) { ResponseHelper.BadRequest(response, "ID inválido"); return; }

            var body = new StreamReader(request.InputStream).ReadToEnd();
            var (ok, error) = _service.CambiarEstado(id.Value, body);
            if (!ok) { ResponseHelper.BadRequest(response, error); return; }
            ResponseHelper.Ok(response, new { message = "Estado actualizado" });
        }

        // /api/facturas/5  → "5"
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
