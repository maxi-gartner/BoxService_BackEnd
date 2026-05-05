using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public static class ResponseHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void WriteResponse(HttpListenerResponse response, int statusCode, object data)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";

        var envelope = new
        {
            success = true,
            data,
            error = (object?)null
        };

        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
    }

    public static void WriteError(HttpListenerResponse response, int statusCode, string message)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";

        var envelope = new
        {
            success = false,
            data = (object?)null,
            error = new
            {
                code = statusCode,
                message
            }
        };

        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
    }

    public static async Task<T?> ReadJsonBodyAsync<T>(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }
}
