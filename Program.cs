using BoxService_BackEnd.Api;
using BoxService_BackEnd.Data;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Services;

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

// ── Repositorios/servicios viejos que ya se re-conectaron ────────────
// Todavía usan DatabaseConnection (estático) en vez de
// PostgresConnectionFactory — se migran de a uno, no hace falta
// reescribirlos para volver a exponerlos.
builder.Services.AddScoped<ClientRepository>();
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<VehicleRepository>();

var app = builder.Build();

// ── Conexión compartida para el código viejo ──────────────────────────
// Server.cs (HttpListener) llamaba esto mismo antes de arrancar. Los
// Repositories de los módulos todavía no migrados a PostgresConnectionFactory
// (Client, Vehicle, Budget, Service, Invoice, Catalog) dependen de que esto
// se llame una vez al inicio, o cualquier query suya rompe con un
// "connection string vacío".
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("BOXSERVICE_CONNECTION_STRING")
    ?? throw new InvalidOperationException("No hay connection string configurada.");
DatabaseConnection.Configure(connectionString);

// ── API Key ────────────────────────────────────────────────────────
// Mismo esquema que el backend viejo: header X-Api-Key obligatorio en
// todo menos "/" y "/health". Se cayó al migrar a ASP.NET Core — esto lo
// vuelve a dejar como estaba, no es un mecanismo nuevo.
const string DefaultApiKey = "boxservice-dev-key";
var apiKey = builder.Configuration["ApiKey"] ?? DefaultApiKey;
if (apiKey == DefaultApiKey)
{
    Console.WriteLine("[AVISO] Usando la API key por defecto de desarrollo. No la uses en un servidor expuesto a internet.");
}

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

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.TrimEnd('/') ?? "";
    var isPublicRoute = path == "" || path.StartsWith("/health");

    if (!isPublicRoute)
    {
        var providedKey = context.Request.Headers["X-Api-Key"].ToString();

        if (string.IsNullOrEmpty(providedKey) || providedKey != apiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                ApiEnvelope<object>.Fail(401, "Missing or invalid API key.")
            );
            return;
        }
    }

    await next();
});

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

// ── Clientes ───────────────────────────────────────────────────────
// Primer módulo funcional migrado de verdad al pipeline de ASP.NET Core.
// Reutiliza el ClientService/ClientRepository ya existentes (la lógica de
// negocio no cambia) — lo único nuevo acá es la capa HTTP: antes recibía
// HttpListenerRequest/Response, ahora habla el contrato REST que ya espera
// el frontend nuevo (web/docs/API_CONTRACT.md), vía ClientDto/VehicleDto.

app.MapGet("/clients", (ClientService service) =>
    ApiEnvelope<object>.Ok(service.List().Select(ClientDto.FromModel)));

app.MapGet("/clients/{id:int}", (int id, ClientService service) =>
{
    var client = service.GetById(id);
    return client is null
        ? Results.Json(ApiEnvelope<object>.Fail(404, "Client not found."), statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(ApiEnvelope<object>.Ok(ClientDto.FromModel(client)));
});

app.MapGet("/clients/{id:int}/vehicles", (int id, ClientService service, VehicleRepository vehicleRepository) =>
{
    var client = service.GetById(id);
    if (client is null)
    {
        return Results.Json(ApiEnvelope<object>.Fail(404, "Client not found."), statusCode: StatusCodes.Status404NotFound);
    }

    var vehicles = vehicleRepository.GetByClientId(id).Select(VehicleDto.FromModel);
    return Results.Ok(ApiEnvelope<object>.Ok(vehicles));
});

app.MapPost("/clients", (ClientCreateRequest request, ClientService service) =>
{
    try
    {
        var created = service.Create(request);
        return Results.Json(
            ApiEnvelope<object>.Ok(ClientDto.FromModel(created)),
            statusCode: StatusCodes.Status201Created
        );
    }
    catch (ArgumentException ex)
    {
        return Results.Json(ApiEnvelope<object>.Fail(400, ex.Message), statusCode: StatusCodes.Status400BadRequest);
    }
});

app.Run();
