using System.Net;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd.Router
{
    public class CatalogRouter
    {
        private readonly CatalogController _controller = new();

        public void Route(HttpListenerContext context)
        {
            var request  = context.Request;
            var response = context.Response;

            var method = request.HttpMethod.ToUpper();
            var path   = request.Url?.AbsolutePath.TrimEnd('/') ?? "";

            if (method == "GET" && path == "/api/catalogo")
            {
                _controller.GetAll(response);
                return;
            }

            if (method == "POST" && path == "/api/catalogo")
            {
                _controller.Create(request, response);
                return;
            }

            if (method == "PATCH" && TryGetId(path, out int idToUpdate))
            {
                _controller.Update(request, response, idToUpdate);
                return;
            }

            if (method == "DELETE" && TryGetId(path, out int idToDelete))
            {
                _controller.Delete(response, idToDelete);
                return;
            }

            ResponseHelper.NotFound(response, "Catalog route not found");
        }

        private static bool TryGetId(string path, out int id)
        {
            id = 0;

            const string prefix = "/api/catalogo/";
            if (!path.StartsWith(prefix))
                return false;

            var segment = path.Substring(prefix.Length).Trim('/');
            return int.TryParse(segment, out id);
        }
    }
}
