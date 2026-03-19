using System.Net;
using System.Text;
using System.Text.Json;

namespace BoxService_BackEnd
{
    public static class ResponseHelper
    {
        public static void Send(HttpListenerResponse response, int statusCode, object body)
        {
            var json  = JsonSerializer.Serialize(body);
            var bytes = Encoding.UTF8.GetBytes(json);

            response.StatusCode      = statusCode;
            response.ContentType     = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }

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
    }
}
