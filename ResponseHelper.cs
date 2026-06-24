using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BoxService_BackEnd
{
    // ResponseHelper es una clase auxiliar.
    // Sirve para que todos los endpoints respondan con el mismo formato:
    // {
    //   success: true/false,
    //   data: ...,
    //   error: ...
    // }
    public static class ResponseHelper
    {
        // Opciones para convertir objetos C# a JSON.
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            // Hace que las propiedades salgan en camelCase.
            // Ejemplo en C#: ServiceId
            // Ejemplo en JSON: serviceId
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

            // false = el JSON sale compacto, sin espacios ni saltos de línea.
            WriteIndented = false
        };

        // Método base para enviar respuestas.
        // Es privado porque no se usa directamente desde controllers o routers.
        // Los demás métodos públicos usan este Send internamente.
        private static void Send(HttpListenerResponse response, int statusCode, object body)
        {
            // Convierte el objeto body a texto JSON.
            var json = JsonSerializer.Serialize(body, JsonOptions);

            // Convierte el JSON a bytes para poder escribirlo en la respuesta HTTP.
            var bytes = Encoding.UTF8.GetBytes(json);

            // Código HTTP de la respuesta.
            // Ejemplo: 200, 201, 400, 404, 500.
            response.StatusCode = statusCode;

            // Indica que la respuesta es JSON con codificación UTF-8.
            response.ContentType = "application/json; charset=utf-8";

            // Indica el tamaño de la respuesta.
            response.ContentLength64 = bytes.Length;

            // Escribe la respuesta en el OutputStream.
            response.OutputStream.Write(bytes, 0, bytes.Length);

            // Cierra la respuesta.
            response.OutputStream.Close();
        }

        // ── Métodos públicos — usar siempre estos ────────────────────

        // Respuesta 200 OK.
        // Se usa cuando algo salió bien.
        // Ejemplo: GET /api/services
        public static void Ok(HttpListenerResponse response, object data)
            => Send(response, 200, new
            {
                success = true,
                data,
                error = (object?)null
            });

        // Respuesta 201 Created.
        // Se usa cuando se creó algo nuevo.
        // Ejemplo: POST /api/services
        public static void Created(HttpListenerResponse response, object data)
            => Send(response, 201, new
            {
                success = true,
                data,
                error = (object?)null
            });

        // Respuesta 400 Bad Request.
        // Se usa cuando el cliente mandó datos mal.
        // Ejemplo: JSON inválido, ID inválido, campo obligatorio faltante.
        public static void BadRequest(HttpListenerResponse response, string message)
            => Send(response, 400, new
            {
                success = false,
                data = (object?)null,
                error = new
                {
                    code = 400,
                    message
                }
            });

        // Respuesta 404 Not Found.
        // Se usa cuando no se encontró la ruta o el recurso.
        // Ejemplo: service no encontrado.
        public static void NotFound(HttpListenerResponse response, string message)
            => Send(response, 404, new
            {
                success = false,
                data = (object?)null,
                error = new
                {
                    code = 404,
                    message
                }
            });

        // Respuesta 500 Internal Server Error.
        // Se usa cuando ocurre un error inesperado del servidor.
        public static void InternalError(
            HttpListenerResponse response,
            string message = "Error interno del servidor"
        )
            => Send(response, 500, new
            {
                success = false,
                data = (object?)null,
                error = new
                {
                    code = 500,
                    message
                }
            });

        // ── Métodos para VehicleController o controllers async ─────────

        // Método genérico para responder éxito.
        // Si el statusCode es 201, devuelve Created.
        // Si no, devuelve Ok.
        public static void WriteResponse(HttpListenerResponse response, int statusCode, object data)
        {
            if (statusCode == 201)
                Created(response, data);
            else
                Ok(response, data);
        }

        // Método genérico para responder errores.
        // Según el statusCode recibido, llama al método correspondiente.
        public static void WriteError(HttpListenerResponse response, int statusCode, string message)
        {
            switch (statusCode)
            {
                case 400:
                    BadRequest(response, message);
                    break;

                case 404:
                    NotFound(response, message);
                    break;

                // 409 Conflict.
                // Se puede usar cuando hay conflicto de datos.
                // Ejemplo: intentar crear algo duplicado.
                case 409:
                    Send(response, 409, new
                    {
                        success = false,
                        data = (object?)null,
                        error = new
                        {
                            code = 409,
                            message
                        }
                    });
                    break;

                // 501 Not Implemented.
                // Se puede usar cuando un endpoint todavía no está implementado.
                case 501:
                    Send(response, 501, new
                    {
                        success = false,
                        data = (object?)null,
                        error = new
                        {
                            code = 501,
                            message
                        }
                    });
                    break;

                // Cualquier otro error cae como error interno.
                default:
                    InternalError(response, message);
                    break;
            }
        }

        // Lee el body JSON de una request y lo convierte a un objeto C#.
        // Es async porque usa ReadToEndAsync().
        // Ejemplo:
        // var data = await ResponseHelper.ReadJsonBodyAsync<Service>(request);
        public static async Task<T?> ReadJsonBodyAsync<T>(HttpListenerRequest request)
        {
            // Lee el body de la request.
            // Si no hay encoding, usa UTF-8 por defecto.
            using var reader = new StreamReader(
                request.InputStream,
                request.ContentEncoding ?? Encoding.UTF8
            );

            var body = await reader.ReadToEndAsync();

            // Si el body viene vacío, devuelve default.
            // Para objetos, normalmente default es null.
            if (string.IsNullOrWhiteSpace(body))
                return default;

            // Convierte el JSON leído a un objeto C# del tipo T.
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
    }
}