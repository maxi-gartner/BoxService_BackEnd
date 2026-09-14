using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

public static class ServiceEndpoints
{
    public static void MapServiceEndpoints(this WebApplication app)
    {
        app.MapGet("/services", (ServicesService service) => Ok(service.GetAll().Select(ServiceDto.FromModel).ToArray()));
        app.MapGet("/services/{id:int}", (int id, ServicesService service) =>
        {
            var value = service.GetById(id);
            return value is null ? Error(404, "Service not found") : Ok(ServiceDto.FromModel(value));
        });
        app.MapGet("/services/{id:int}/details", (int id, ServicesService service) =>
            service.GetById(id) is null ? Error(404, "Service not found") : Ok(service.GetDetails(id)));
        app.MapPost("/services", (ServiceCreateRequest request, ServicesService service) => Write(() =>
            Ok(ServiceDto.FromModel(service.Create(new Service
            {
                VehicleId = request.VehicleId,
                Date = request.Date,
                Mileage = request.Mileage,
                ServiceType = request.ServiceType,
                Notes = request.Notes
            })), 201)));
        app.MapPost("/services/{id:int}/details", (int id, ServiceDetailCreateRequest request, ServicesService service) => Write(() =>
        {
            if (service.GetById(id) is null) return Error(404, "Service not found");
            return Ok(service.CreateDetail(new ServiceDetail
            {
                ServiceId = id,
                Description = request.Description,
                Done = request.Done
            }), 201);
        }));
    }
}
