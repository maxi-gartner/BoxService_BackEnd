using BoxService_BackEnd.Api;
using BoxService_BackEnd.Data;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5001");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<PostgresConnectionFactory>();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(
            ApiEnvelope<object>.Fail(500, "Error interno del servidor")
        );
    });
});

app.UseCors();

app.MapGet("/", () => ApiEnvelope<object>.Ok(new
{
    name = "BoxService API",
    framework = "ASP.NET Core",
    version = "2.0.0"
}));

app.MapGet("/health", async (
    PostgresConnectionFactory connectionFactory,
    CancellationToken cancellationToken
) =>
{
    try
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        return Results.Ok(ApiEnvelope<object>.Ok(new
        {
            status = "healthy",
            database = "connected",
            timestamp = DateTime.UtcNow,
            version = "2.0.0"
        }));
    }
    catch
    {
        return Results.Json(
            ApiEnvelope<object>.Fail(503, "Database unreachable"),
            statusCode: StatusCodes.Status503ServiceUnavailable
        );
    }
});

app.Run();
