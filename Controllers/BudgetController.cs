using System.IO;
using System.Net;
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
            if (id == null) { ResponseHelper.BadRequest(response, "Invalid ID"); return; }

            var result = _service.GetById(id.Value);
            if (result == null) { ResponseHelper.NotFound(response, "Budget not found"); return; }

            ResponseHelper.Ok(response, result);
        }

        public void Create(HttpListenerRequest request, HttpListenerResponse response)
        {
            var body = new StreamReader(request.InputStream).ReadToEnd();
            var (ok, error, result) = _service.Create(body);
            if (!ok) { ResponseHelper.BadRequest(response, error); return; }
            ResponseHelper.Created(response, result!);
        }

        public void UpdateStatus(HttpListenerRequest request, HttpListenerResponse response)
        {
            var id = ParseId(request.Url?.AbsolutePath, 3);
            if (id == null) { ResponseHelper.BadRequest(response, "Invalid ID"); return; }

            var body = new StreamReader(request.InputStream).ReadToEnd();
            var (ok, error) = _service.UpdateStatus(id.Value, body);
            if (!ok) { ResponseHelper.BadRequest(response, error); return; }
            ResponseHelper.Ok(response, new { message = "Status updated" });
        }

        public void Approve(HttpListenerRequest request, HttpListenerResponse response)
        {
            var id = ParseId(request.Url?.AbsolutePath, 3);
            if (id == null) { ResponseHelper.BadRequest(response, "Invalid ID"); return; }

            var (ok, error, result) = _service.Approve(id.Value);
            if (!ok) { ResponseHelper.BadRequest(response, error); return; }
            ResponseHelper.Created(response, result!);
        }

        // /api/budgets/5/approve → segment 3 = "5"
        private static int? ParseId(string? path, int segment)
        {
            if (path == null) return null;
            var parts = path.Trim('/').Split('/');
            if (parts.Length > segment - 1 && int.TryParse(parts[segment - 1], out var id))
                return id;
            return null;
        }
    }
}
