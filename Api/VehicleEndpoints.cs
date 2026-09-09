using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using Npgsql;

namespace BoxService_BackEnd.Api;

public static class VehicleEndpoints
{
    public static void MapVehicleEndpoints(this WebApplication app)
    {
        app.MapGet("/vehicles", (string? plate, VehicleService service) =>
        {
            if (string.IsNullOrWhiteSpace(plate))
                return Results.Ok(ApiEnvelope<object>.Ok(service.List().Select(VehicleDto.FromModel)));

            var vehicle = service.FindByPlate(plate);
            return vehicle is null
                ? Error(404, "Vehicle not found.")
                : Results.Ok(ApiEnvelope<object>.Ok(VehicleDto.FromModel(vehicle)));
        });

        app.MapGet("/vehicles/{id:int}", (int id, VehicleService service) =>
        {
            var vehicle = service.GetById(id);
            return vehicle is null
                ? Error(404, "Vehicle not found.")
                : Results.Ok(ApiEnvelope<object>.Ok(VehicleDto.FromModel(vehicle)));
        });

        app.MapGet("/vehicles/{id:int}/history", (int id, VehicleService vehicles, ServicesService services) =>
        {
            if (vehicles.GetById(id) is null)
                return Error(404, "Vehicle not found.");

            return Results.Ok(ApiEnvelope<object>.Ok(services.GetByVehicleId(id).Select(ServiceDto.FromModel)));
        });

        app.MapPost("/vehicles", (VehicleCreateRequest request, VehicleService service) =>
        {
            try
            {
                var vehicle = service.Create(request);
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
