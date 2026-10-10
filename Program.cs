using System.Text;
using Microsoft.AspNetCore.Identity;
using BoxService_BackEnd.Auth;
using BoxService_BackEnd.Api;
using BoxService_BackEnd.Data;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Services;
using BoxService_BackEnd.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// dotnet run -- hash-password "la-contraseña": imprime el hash para pegar
// en Jwt:Users (local) o cargar como Jwt__Users__N__PasswordHash (Render).
// No levanta el servidor. El README la documentaba pero se había perdido
// en un merge — el setup de secretos de Render la necesita para generar
// los 3 hashes de las cuentas demo.
if (args.Length == 2 && args[0] == "hash-password")
{
    Console.WriteLine(new PasswordHasher<AuthUser>().HashPassword(new AuthUser(), args[1]));
    return;
}

// dotnet run -- migrate: aplica Database/migrations/*.sql contra la base de
// appsettings.json y termina, sin levantar el servidor web. Cada archivo usa
// CREATE TABLE/INDEX IF NOT EXISTS (o ALTER ... IF NOT EXISTS), así que
// correrlo de nuevo sobre una base que ya tiene las tablas no rompe nada:
// las migraciones ya aplicadas se saltean sin error.
if (args.Length > 0 && args[0] == "migrate")
{
    var migrateConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? Environment.GetEnvironmentVariable("BOXSERVICE_CONNECTION_STRING")
        ?? throw new InvalidOperationException("No hay connection string configurada.");
    await DatabaseSetup.SetupAsync(migrateConnectionString);
    return;
}

builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options =>
    options.ThrowOnBadRequest = false);

// Por defecto ASP.NET Core loguea en nivel Info cada request completo
// (6 líneas por pedido: "Request starting", "Executing endpoint",
// "Setting status code", "Writing value", "Executed endpoint", "Request
// finished") — contra /health, que Render pinguea cada pocos segundos en
// 2 servicios, eso ahoga cualquier log real (como el LogWarning de
// PortalAuthService cuando Google rechaza un token). Subir el piso de
// Microsoft.AspNetCore a Warning saca esos 6 logs por request de en
// medio, sin tocar ningún Warning/Error — que es justo lo que importa ver.
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

// PORT solo la setea Render (u otro PaaS similar) — si está, escuchamos en
// 0.0.0.0:$PORT porque el health check le pega desde afuera del contenedor.
// Sin PORT (dev local) seguimos en localhost:5001 como siempre: bindear
// 0.0.0.0 en Windows dispara el prompt del Firewall en cada arranque, y en
// máquinas de laburo con permisos restringidos ni se puede aceptar.
var renderPort = Environment.GetEnvironmentVariable("PORT");
builder.WebHost.UseUrls(renderPort is not null ? $"http://0.0.0.0:{renderPort}" : "http://localhost:5001");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var authOptions = builder.Configuration.GetSection("Jwt").Get<AuthOptions>()
    ?? throw new InvalidOperationException("No hay configuración Jwt.");

if (string.IsNullOrWhiteSpace(authOptions.Key) || Encoding.UTF8.GetByteCount(authOptions.Key) < 32)
{
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 bytes.");
}

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<AuthService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

// Cors:AllowedOrigin (env var Cors__AllowedOrigin) restringe el origen una
// vez que hay un frontend público real que proteger (Vercel). Sin setear,
// sigue permitiendo cualquier origen — no rompe nada en desarrollo local.
var allowedOrigin = builder.Configuration["Cors:AllowedOrigin"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (string.IsNullOrWhiteSpace(allowedOrigin))
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigin).AllowAnyHeader().AllowAnyMethod();
        }
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
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<BudgetRepository>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<ServiceInspectionRepository>();
builder.Services.AddScoped<ServiceInspectionService>();
builder.Services.AddSingleton<SupabaseStorageService>();
builder.Services.AddScoped<ServicesService>();
builder.Services.AddScoped<CatalogService>();

// ── Portal del cliente (login con Google, ver estado del vehículo) ───
builder.Services.AddScoped<PortalAccessRepository>();
builder.Services.AddScoped<PortalService>();
builder.Services.AddScoped<PortalAuthService>();

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

if (args.Contains("--setup-db", StringComparer.OrdinalIgnoreCase))
{
    await DatabaseSetup.SetupAsync(connectionString);
    return;
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
app.UseAuthentication();
app.UseAuthorization();

app.UseStatusCodePages(async statusContext =>
{
    var response = statusContext.HttpContext.Response;
    await response.WriteAsJsonAsync(ApiEnvelope<object>.Fail(
        response.StatusCode,
        response.StatusCode == 404 ? "Route not found." : "Invalid request."));
});

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.TrimEnd('/') ?? "";
    var isPublicRoute = path == "" || path.StartsWith("/health") || path == "/auth/login"
        || path == "/portal/auth/google";

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

    // El portal del cliente (role "customer") es un mundo aparte del
    // staff: solo puede pisar /portal/*, y nada del staff puede pisar
    // /portal/* salvo que sea "customer". Sin esto, cualquier JWT
    // autenticado (de cualquier lado) entraría a todo lo demás, porque
    // el resto de las rutas de acá arriba solo chequean IsAuthenticated.
    var isPortalRoute = path.StartsWith("/portal", StringComparison.OrdinalIgnoreCase)
        && path != "/portal/auth/google";
    if (!isPublicRoute)
    {
        if (isPortalRoute && role != "customer")
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiEnvelope<object>.Fail(403, "Esto es solo para el portal del cliente."));
            return;
        }

        if (!isPortalRoute && role == "customer")
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiEnvelope<object>.Fail(403, "Esto es solo para el equipo del taller."));
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

// ── Clientes ───────────────────────────────────────────────────────
// Primer módulo funcional migrado de verdad al pipeline de ASP.NET Core.
// Reutiliza el ClientService/ClientRepository ya existentes (la lógica de
// negocio no cambia) — lo único nuevo acá es la capa HTTP: antes recibía
// HttpListenerRequest/Response, ahora habla el contrato REST que ya espera
// el frontend nuevo (web/docs/API_CONTRACT.md), vía ClientDto/VehicleDto.

app.MapGet("/clients", (ClientService service) =>
    ApiEnvelope<object>.Ok(service.List().Select(ClientDto.FromModel))).RequireAuthorization();

app.MapGet("/clients/{id:int}", (int id, ClientService service) =>
{
    var client = service.GetById(id);
    return client is null
        ? Results.Json(ApiEnvelope<object>.Fail(404, "Client not found."), statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(ApiEnvelope<object>.Ok(ClientDto.FromModel(client)));
}).RequireAuthorization();

app.MapGet("/clients/{id:int}/vehicles", (int id, ClientService service, VehicleRepository vehicleRepository) =>
{
    var client = service.GetById(id);
    if (client is null)
    {
        return Results.Json(ApiEnvelope<object>.Fail(404, "Client not found."), statusCode: StatusCodes.Status404NotFound);
    }

    var vehicles = vehicleRepository.GetByClientId(id).Select(VehicleDto.FromModel);
    return Results.Ok(ApiEnvelope<object>.Ok(vehicles));
}).RequireAuthorization();

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
}).RequireAuthorization();

app.MapVehicleEndpoints();
app.MapBudgetEndpoints();
app.MapServiceEndpoints();
app.MapServiceInspectionEndpoints();
app.MapInvoiceEndpoints();
app.MapCatalogEndpoints();
app.MapPortalEndpoints();

app.Run();
