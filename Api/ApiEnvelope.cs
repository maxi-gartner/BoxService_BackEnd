using System.Text.Json.Serialization;

namespace BoxService_BackEnd.Api;

public sealed class ApiEnvelope<T>
{
    public required bool Success { get; init; }

    public T? Data { get; init; }

    public ApiError? Error { get; init; }

    public static ApiEnvelope<T> Ok(T data) => new()
    {
        Success = true,
        Data = data,
        Error = null
    };

    public static ApiEnvelope<T> Fail(int code, string message) => new()
    {
        Success = false,
        Data = default,
        Error = new ApiError(code, message)
    };
}

public sealed record ApiError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message
);
