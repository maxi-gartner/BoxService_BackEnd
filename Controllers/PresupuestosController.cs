using System.IO;
using System.Net;
using BoxService_BackEnd.Services;

namespace BoxService_BackEnd.Controllers
{
    public class PresupuestosController
    {
        private readonly PresupuestosService _service = new();

        public void GetAll(HttpListenerResponse response)
        {
            var lista = _service.GetAll();
            ResponseHelper.Ok(response, lista);
        }

        public void GetById(HttpListenerRequest request, HttpListenerResponse response)
        {
            var id = ParseIdFromSegment(request.Url?.AbsolutePath, 3);
            if (id == null) { ResponseHelper.BadRequest(response, "ID inválido"); return; }

            var resultado = _service.GetById(id.Value);
            if (resultado == null) { ResponseHelper.NotFound(response, "Presupuesto no encontrado"); return; }

            ResponseHelper.Ok(response, resultado);
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
            var id = ParseIdFromSegment(request.Url?.AbsolutePath, 3);
            if (id == null) { ResponseHelper.BadRequest(response, "ID inválido"); return; }

            var body = new StreamReader(request.InputStream).ReadToEnd();
            var (ok, error) = _service.CambiarEstado(id.Value, body);
            if (!ok) { ResponseHelper.BadRequest(response, error); return; }
            ResponseHelper.Ok(response, new { message = "Estado actualizado" });
        }

        public void Aprobar(HttpListenerRequest request, HttpListenerResponse response)
        {
            var id = ParseIdFromSegment(request.Url?.AbsolutePath, 3);
            if (id == null) { ResponseHelper.BadRequest(response, "ID inválido"); return; }

            var (ok, error, resultado) = _service.Aprobar(id.Value);
            if (!ok) { ResponseHelper.BadRequest(response, error); return; }
            ResponseHelper.Created(response, resultado!);
        }

        // Extrae el ID del segmento en posición indicada
        // /api/presupuestos/5/aprobar → segmento 3 = "5"
        private static int? ParseIdFromSegment(string? path, int segment)
        {
            if (path == null) return null;
            var parts = path.Trim('/').Split('/');
            if (parts.Length > segment - 1 && int.TryParse(parts[segment - 1], out var id))
                return id;
            return null;
        }
    }
}
