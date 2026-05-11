using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BoxService_BackEnd
{
    public static class ResponseHelper
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Método base — privado, nadie lo llama directamente
        private static void Send(HttpListenerResponse response, int statusCode, object body)
        {
            var json  = JsonSerializer.Serialize(body, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            response.StatusCode      = statusCode;
            response.ContentType     = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }

        // ── Métodos públicos — usar siempre estos ────────────────────

        public static void Ok(HttpListenerResponse response, object data)
            => Send(response, 200, new { success = true, data, error = (object?)null });

        public static void Created(HttpListenerResponse response, object data)
            => Send(response, 201, new { success = true, data, error = (object?)null });

        public static void BadRequest(HttpListenerResponse response, string message)
            => Send(response, 400, new { success = false, data = (object?)null, error = new { code = 400, message } });

        public static void NotFound(HttpListenerResponse response, string message)
            => Send(response, 404, new { success = false, data = (object?)null, error = new { code = 404, message } });

        public static void InternalError(HttpListenerResponse response, string message = "Error interno del servidor")
            => Send(response, 500, new { success = false, data = (object?)null, error = new { code = 500, message } });

        // ── Métodos para VehicleController (async) ───────────────────

        public static void WriteResponse(HttpListenerResponse response, int statusCode, object data)
        {
            if (statusCode == 201)
                Created(response, data);
            else
                Ok(response, data);
        }

        public static void WriteError(HttpListenerResponse response, int statusCode, string message)
        {
            switch (statusCode)
            {
                case 400: BadRequest(response, message);  break;
                case 404: NotFound(response, message);    break;
                case 409: Send(response, 409, new { success = false, data = (object?)null, error = new { code = 409, message } }); break;
                case 501: Send(response, 501, new { success = false, data = (object?)null, error = new { code = 501, message } }); break;
                default:  InternalError(response, message); break;
            }
        }

        public static async Task<T?> ReadJsonBodyAsync<T>(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body)) return default;
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
    }
}