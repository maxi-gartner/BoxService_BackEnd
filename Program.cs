using System.Text;
using BoxService_BackEnd.Auth;
using BoxService_BackEnd.Api;
using BoxService_BackEnd.Data;
using BoxService_BackEnd.Database;
using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options =>
    options.ThrowOnBadRequest = false);

builder.WebHost.UseUrls("http://localhost:5001");

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
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = authOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.Key)),
            ValidateLifetime = true,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

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
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<ServicesService>();
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
    var isPublicRoute = path == "" || path.StartsWith("/health") || path == "/auth/login";

    if (!isPublicRoute && context.User.Identity?.IsAuthenticated != true)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(ApiEnvelope<object>.Fail(401, "Missing or invalid bearer token."));
        return;
    }

    var isCatalogWrite = path.StartsWith("/api/catalogo", StringComparison.OrdinalIgnoreCase)
        && !HttpMethods.IsGet(context.Request.Method);
    var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
    if (isCatalogWrite && role is not ("dueno" or "superadmin"))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(ApiEnvelope<object>.Fail(403, "No tenés permisos para modificar el catálogo."));
        return;
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
    role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
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

// ── Vehículos ──────────────────────────────────────────────────────
app.MapGet("/api/vehiculos", (string? plate, [FromServices] VehicleService service) =>
{
    if (!string.IsNullOrWhiteSpace(plate))
    {
        var vehicle = service.FindByPlate(plate.Trim());
        return vehicle is null
            ? Results.Json(ApiEnvelope<object>.Fail(404, "Vehicle not found."), statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(ApiEnvelope<object>.Ok(VehicleDto.FromModel(vehicle)));
    }

    return Results.Ok(ApiEnvelope<object>.Ok(service.List().Select(VehicleDto.FromModel)));
});

app.MapGet("/api/vehiculos/{id:int}", (int id, [FromServices] VehicleService service) =>
{
    var vehicle = service.GetById(id);
    return vehicle is null
        ? Results.Json(ApiEnvelope<object>.Fail(404, "Vehicle not found."), statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(ApiEnvelope<object>.Ok(VehicleDto.FromModel(vehicle)));
});

app.MapPost("/api/vehiculos", (VehicleCreateRequest request, [FromServices] VehicleService service) =>
{
    try
    {
        return Results.Json(
            ApiEnvelope<object>.Ok(VehicleDto.FromModel(service.Create(request))),
            statusCode: StatusCodes.Status201Created);
    }
    catch (ArgumentException ex)
    {
        return Results.Json(ApiEnvelope<object>.Fail(400, ex.Message), statusCode: StatusCodes.Status400BadRequest);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Json(ApiEnvelope<object>.Fail(400, ex.Message), statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapGet("/api/vehiculos/{id:int}/historial", (int id, [FromServices] VehicleService vehicleService, [FromServices] ServicesService servicesService) =>
{
    if (vehicleService.GetById(id) is null)
    {
        return Results.Json(ApiEnvelope<object>.Fail(404, "Vehicle not found."), statusCode: StatusCodes.Status404NotFound);
    }

    return Results.Ok(ApiEnvelope<object>.Ok(servicesService.GetByVehicleId(id)));
});

// ── Presupuestos ─────────────────────────────────────────────────
app.MapGet("/api/budgets", (BudgetService service) =>
    ApiEnvelope<object>.Ok(service.GetAll()));

app.MapGet("/api/budgets/{id:int}", (int id, BudgetService service) =>
{
    var budget = service.GetById(id);
    return budget is null
        ? Results.Json(ApiEnvelope<object>.Fail(404, "Budget not found"), statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(ApiEnvelope<object>.Ok(budget));
});

app.MapPost("/api/budgets", (BudgetCreateRequest request, BudgetService service) =>
{
    var (ok, _, error, result) = service.Create(request);
    return ok
        ? Results.Json(ApiEnvelope<object>.Ok(result), statusCode: StatusCodes.Status201Created)
        : Results.Json(ApiEnvelope<object>.Fail(400, error), statusCode: StatusCodes.Status400BadRequest);
});

app.MapPatch("/api/budgets/{id:int}", (int id, BudgetStatusRequest request, BudgetService service) =>
{
    var (ok, notFound, error, result) = service.UpdateStatus(id, request);
    if (ok) return Results.Ok(ApiEnvelope<object>.Ok(result));

    return Results.Json(
        ApiEnvelope<object>.Fail(notFound ? 404 : 400, error),
        statusCode: notFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
});

app.MapPut("/api/budgets/{id:int}/service", (int id, AssignServiceRequest request, BudgetService service) =>
{
    var (ok, notFound, error) = service.AssignService(id, request.ServiceId);
    if (ok) return Results.Ok(ApiEnvelope<object>.Ok(new { message = "Budget linked to service" }));

    return Results.Json(
        ApiEnvelope<object>.Fail(notFound ? 404 : 400, error),
        statusCode: notFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
});

// ── Facturas ──────────────────────────────────────────────────────
app.MapGet("/api/invoices", (InvoiceService service) =>
    ApiEnvelope<object>.Ok(service.GetAll()));

app.MapGet("/api/invoices/{id:int}", (int id, InvoiceService service) =>
{
    var invoice = service.GetById(id);
    return invoice is null
        ? Results.Json(ApiEnvelope<object>.Fail(404, "Invoice not found"), statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(ApiEnvelope<object>.Ok(invoice));
});

app.MapPost("/api/invoices", (InvoiceCreateRequest request, InvoiceService service) =>
{
    var (ok, _, error, result) = service.Create(request);
    return ok
        ? Results.Json(ApiEnvelope<object>.Ok(result), statusCode: StatusCodes.Status201Created)
        : Results.Json(ApiEnvelope<object>.Fail(400, error), statusCode: StatusCodes.Status400BadRequest);
});

app.MapPatch("/api/invoices/{id:int}", (int id, InvoiceStatusRequest request, InvoiceService service) =>
{
    var (ok, notFound, error, result) = service.UpdateStatus(id, request);
    if (ok) return Results.Ok(ApiEnvelope<object>.Ok(result));

    return Results.Json(
        ApiEnvelope<object>.Fail(notFound ? 404 : 400, error),
        statusCode: notFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
});

// ── Services ──────────────────────────────────────────────────────
app.MapGet("/api/services", (ServicesService service) =>
    ApiEnvelope<object>.Ok(service.GetAll()));

app.MapGet("/api/services/{id:int}", (int id, ServicesService service) =>
{
    var item = service.GetById(id);
    return item is null
        ? Results.Json(ApiEnvelope<object>.Fail(404, "Service not found"), statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(ApiEnvelope<object>.Ok(item));
});

app.MapPost("/api/services", (ServiceCreateRequest request, ServicesService service) =>
{
    try
    {
        var created = service.Create(new Service
        {
            Date = request.Date,
            Mileage = request.Mileage,
            ServiceType = request.ServiceType,
            Notes = request.Notes,
            VehicleId = request.VehicleId,
        });
        return Results.Json(ApiEnvelope<object>.Ok(created), statusCode: StatusCodes.Status201Created);
    }
    catch (ArgumentException ex)
    {
        return Results.Json(ApiEnvelope<object>.Fail(400, ex.Message), statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapGet("/api/services/{id:int}/details", (int id, ServicesService service) =>
    ApiEnvelope<object>.Ok(service.GetDetails(id)));

app.MapPost("/api/services/{id:int}/details", (int id, ServiceDetail request, ServicesService service) =>
{
    request.ServiceId = id;
    try
    {
        return Results.Json(ApiEnvelope<object>.Ok(service.CreateDetail(request)), statusCode: StatusCodes.Status201Created);
    }
    catch (ArgumentException ex)
    {
        return Results.Json(ApiEnvelope<object>.Fail(400, ex.Message), statusCode: StatusCodes.Status400BadRequest);
    }
});

// ── Catálogo ──────────────────────────────────────────────────────
app.MapGet("/api/catalogo", (CatalogService service) =>
    ApiEnvelope<object>.Ok(service.GetAll()));

app.MapPost("/api/catalogo", (CatalogItemCreateRequest request, CatalogService service) =>
{
    try
    {
        return Results.Json(ApiEnvelope<object>.Ok(service.Create(request)), statusCode: StatusCodes.Status201Created);
    }
    catch (ArgumentException ex)
    {
        return Results.Json(ApiEnvelope<object>.Fail(400, ex.Message), statusCode: StatusCodes.Status400BadRequest);
    }
});

app.MapPatch("/api/catalogo/{id:int}", (int id, CatalogItemUpdateRequest request, CatalogService service) =>
{
    var (ok, notFound, error) = service.Update(id, request);
    if (ok) return Results.Ok(ApiEnvelope<object>.Ok(new { message = "Catalog item updated" }));

    return Results.Json(
        ApiEnvelope<object>.Fail(notFound ? 404 : 400, error),
        statusCode: notFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
});

app.MapDelete("/api/catalogo/{id:int}", (int id, CatalogService service) =>
{
    return service.Delete(id)
        ? Results.Ok(ApiEnvelope<object>.Ok(new { message = "Catalog item deleted" }))
        : Results.Json(ApiEnvelope<object>.Fail(404, "Catalog item not found"), statusCode: StatusCodes.Status404NotFound);
});

app.MapVehicleEndpoints();
app.MapBudgetEndpoints();
app.MapServiceEndpoints();
app.MapInvoiceEndpoints();
app.MapCatalogEndpoints();

app.Run();
