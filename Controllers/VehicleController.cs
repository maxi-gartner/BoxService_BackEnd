using System;
using System.IO;
using System.Net;
using System.Text.Json;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;

namespace BoxService_BackEnd.Controllers
{
    public class VehicleController
    {
        private readonly VehicleService _service;
        private readonly ServicesService _servicesService;

        public VehicleController(VehicleService service, ServicesService servicesService)
        {
            _service = service;
            _servicesService = servicesService;
        }

        // GET /api/vehiculos            -> lista completa
        // GET /api/vehiculos?plate=ABC   -> filtra por patente (reemplaza al viejo /vehiculos/buscar)
        public void GetAll(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                var plate = request.QueryString["plate"];

                if (!string.IsNullOrWhiteSpace(plate))
                {
                    var vehicle = _service.FindByPlate(plate.Trim());
                    if (vehicle is null) { ResponseHelper.NotFound(response, "Vehicle not found."); return; }
                    ResponseHelper.Ok(response, vehicle);
                    return;
                }

                ResponseHelper.Ok(response, _service.List());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing vehicles: {ex}");
                ResponseHelper.InternalError(response);
            }
        }

        public void GetById(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            try
            {
                var vehicle = _service.GetById(id);
                if (vehicle is null) { ResponseHelper.NotFound(response, "Vehicle not found."); return; }
                ResponseHelper.Ok(response, vehicle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting vehicle: {ex}");
                ResponseHelper.InternalError(response);
            }
        }

        public void Create(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream))
                body = reader.ReadToEnd();

            VehicleCreateRequest? req;
            try
            {
                req = JsonSerializer.Deserialize<VehicleCreateRequest>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                ResponseHelper.BadRequest(response, "Invalid or empty JSON body.");
                return;
            }

            if (req is null) { ResponseHelper.BadRequest(response, "Invalid JSON body."); return; }

            try
            {
                var created = _service.Create(req);
                ResponseHelper.Created(response, created);
            }
            catch (ArgumentException ex)        { ResponseHelper.BadRequest(response, ex.Message); }
            catch (InvalidOperationException ex) { ResponseHelper.BadRequest(response, ex.Message); }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating vehicle: {ex}");
                ResponseHelper.InternalError(response);
            }
        }

        // GET /api/vehiculos/{id}/historial
        public void GetHistory(HttpListenerResponse response, int vehicleId)
        {
            try
            {
                var vehicle = _service.GetById(vehicleId);
                if (vehicle is null) { ResponseHelper.NotFound(response, "Vehicle not found."); return; }

                var history = _servicesService.GetByVehicleId(vehicleId);
                ResponseHelper.Ok(response, history);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting vehicle history: {ex}");
                ResponseHelper.InternalError(response);
            }
        }
    }
}
