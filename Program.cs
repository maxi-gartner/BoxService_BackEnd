using System.Text;
using BoxService_BackEnd.Api;
using BoxService_BackEnd.Auth;
using BoxService_BackEnd.Data;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

// ── Utilidad de desarrollo: generar el hash de una contraseña para pegar
// en appsettings.json (sección Jwt:Users). No levanta el servidor.
// Uso: dotnet run -- hash-password "la-contraseña"
if (args.Length == 2 && args[0] == "hash-password")
{
    Console.WriteLine(new PasswordHasher<object>().HashPassword(new object(), args[1]));
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options =>
    options.ThrowOnBadRequest = false);

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

// ── Autenticación (JWT) ────────────────────────────────────────────
// Reemplaza el X-Api-Key compartido: ahora cada usuario tiene su propio
// login (appsettings.json → Jwt:Users) y token, con rol incluido en el
// claim. La lista de usuarios sigue siendo config estática (no hay tabla
// de usuarios en la base todavía) — alcanza para lo que pide el TP.
var authOptions = builder.Configuration.GetSection("Jwt").Get<AuthOptions>()
    ?? throw new InvalidOperationException("No hay configuración Jwt (ver appsettings.example.json).");

if (string.IsNullOrWhiteSpace(authOptions.Key) || Encoding.UTF8.GetByteCount(authOptions.Key) < 32)
{
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 bytes.");
}

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<AuthService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // MapInboundClaims = false: sin esto, ASP.NET Core remapea algunos
        // nombres de claim conocidos a las URIs largas de ClaimTypes al
        // leer el token — con "role"/"name" en el JWT (ver AuthService.cs)
        // eso dejaría el claim con un .Type distinto al que se buscó al
        // escribirlo. Mejor que quede tal cual el token, sin sorpresas.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = authOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.Key)),
            ValidateLifetime = true,
            RoleClaimType = "role",
            NameClaimType = "name",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<PostgresConnectionFactory>();

// ── Repositorios/servicios viejos que ya se re-conectaron ────────────
// Todavía usan DatabaseConnection (estático) en vez de
// PostgresConnectionFactory — se migran de a uno, no hace falta
// reescribirlos para volver a exponerlos.
builder.Services.AddScoped<ClientRepository>();
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<VehicleRepository>();
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<ServicesService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<CatalogService>();

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

app.UseStatusCodePages(async statusContext =>
{
    var response = statusContext.HttpContext.Response;
    await response.WriteAsJsonAsync(ApiEnvelope<object>.Fail(
        response.StatusCode,
        response.StatusCode == 404 ? "Route not found." : "Invalid request."));
});

app.UseAuthentication();
app.UseAuthorization();

// ── Autenticación / autorización ──────────────────────────────────
// Reemplaza el X-Api-Key: todo lo que no sea "/", "/health" o
// "/auth/login" exige un Bearer token válido (ver Auth/AuthService.cs).
// Además, escribir en el catálogo (POST/PATCH/DELETE) queda restringido
// a dueño/superadmin — es el único permiso por rol que pidió el TP hasta
// ahora; el resto de los módulos solo pide estar logueado.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.TrimEnd('/') ?? "";
    var isPublicRoute = path == "" || path.StartsWith("/health") || path == "/auth/login";

    if (!isPublicRoute && context.User.Identity?.IsAuthenticated != true)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(ApiEnvelope<object>.Fail(401, "Missing or invalid bearer token."));
        return;
    }

    var isCatalogWrite = path.StartsWith("/catalog", StringComparison.OrdinalIgnoreCase)
        && !HttpMethods.IsGet(context.Request.Method);
    var role = context.User.FindFirst("role")?.Value;
    if (isCatalogWrite && role is not ("dueno" or "superadmin"))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(ApiEnvelope<object>.Fail(403, "No tenés permisos para modificar el catálogo."));
        return;
    }

    await next();
});

app.MapPost("/auth/login", (LoginRequest request, AuthService authService) =>
{
    var result = authService.Authenticate(request.Username, request.Password);
    return result is null
        ? Results.Json(ApiEnvelope<object>.Fail(401, "Usuario o contraseña inválidos."), statusCode: StatusCodes.Status401Unauthorized)
        : Results.Ok(ApiEnvelope<object>.Ok(result));
});

app.MapGet("/auth/me", (HttpContext context) => ApiEnvelope<object>.Ok(new
{
    username = context.User.Identity?.Name,
    role = context.User.FindFirst("role")?.Value,
}));

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

app.MapVehicleEndpoints();
app.MapBudgetEndpoints();
app.MapServiceEndpoints();
app.MapInvoiceEndpoints();
app.MapCatalogEndpoints();

app.Run();
