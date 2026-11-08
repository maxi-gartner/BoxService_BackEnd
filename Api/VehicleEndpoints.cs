using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using Npgsql;

namespace BoxService_BackEnd.Api;

public static class VehicleEndpoints
{
    public static void MapVehicleEndpoints(this WebApplication app)
    {
        app.MapGet("/vehicles", (string? plate, int? page, int? pageSize, string? search, HttpContext context, VehicleService service) =>
        {
            var tenantId = context.TenantId();
            if (!string.IsNullOrWhiteSpace(plate))
            {
                var vehicle = service.FindByPlate(plate, tenantId);
                return vehicle is null
                    ? Error(404, "Vehicle not found.")
                    : Results.Ok(ApiEnvelope<object>.Ok(VehicleDto.FromModel(vehicle)));
            }

            var (items, total) = service.ListPaged(tenantId, page ?? 1, pageSize ?? 25, search);
            return Results.Ok(ApiEnvelope<object>.Ok(new PagedResult<VehicleDto>
            {
                Items = items.Select(i => VehicleDto.FromModel(i.Vehicle, i.ClientName)).ToList(),
                Page = page ?? 1,
                PageSize = pageSize ?? 25,
                TotalCount = total
            }));
        });

        app.MapGet("/vehicles/{id:int}", (int id, HttpContext context, VehicleService service) =>
        {
            var vehicle = service.GetById(id, context.TenantId());
            return vehicle is null
                ? Error(404, "Vehicle not found.")
                : Results.Ok(ApiEnvelope<object>.Ok(VehicleDto.FromModel(vehicle)));
        });

        app.MapGet("/vehicles/{id:int}/history", (int id, HttpContext context, VehicleService vehicles, ServicesService services) =>
        {
            // GetByVehicleId no filtra por tenant (lo usa también el
            // portal del cliente) — acá alcanza con que el vehículo ya se
            // haya confirmado del tenant del caller: ningún service puede
            // apuntar a un vehículo de otro tenant.
            if (vehicles.GetById(id, context.TenantId()) is null)
                return Error(404, "Vehicle not found.");

            return Results.Ok(ApiEnvelope<object>.Ok(services.GetByVehicleId(id).Select(ServiceDto.FromModel)));
        });

        app.MapPost("/vehicles", (VehicleCreateRequest request, HttpContext context, VehicleService service) =>
        {
            try
            {
                var vehicle = service.Create(context.TenantId(), request);
                return Results.Json(ApiEnvelope<object>.Ok(VehicleDto.FromModel(vehicle)), statusCode: 201);
            }
            catch (ArgumentException ex)
            {
                return Error(400, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Error(400, ex.Message);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                return Error(400, "Client does not exist.");
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return Error(400, "A vehicle with that plate already exists.");
            }
        });
    }

    private static IResult Error(int code, string message) =>
        Results.Json(ApiEnvelope<object>.Fail(code, message), statusCode: code);
}
