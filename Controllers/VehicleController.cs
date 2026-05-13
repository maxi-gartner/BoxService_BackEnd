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

        public VehicleController(VehicleService service)
        {
            _service = service;
        }

        public void GetAll(HttpListenerResponse response)
        {
            var vehicles = _service.List();
            ResponseHelper.Ok(response, vehicles);
        }

        public void GetById(HttpListenerRequest request, HttpListenerResponse response, int id)
        {
            var vehicle = _service.GetById(id);
            if (vehicle is null) { ResponseHelper.NotFound(response, "Vehicle not found."); return; }
            ResponseHelper.Ok(response, vehicle);
        }

        public void SearchByPlate(HttpListenerRequest request, HttpListenerResponse response)
        {
            var plate = request.QueryString["plate"];
            if (string.IsNullOrWhiteSpace(plate))
            {
                ResponseHelper.BadRequest(response, "Query param 'plate' is required.");
                return;
            }

            var vehicle = _service.FindByPlate(plate.Trim());
            if (vehicle is null) { ResponseHelper.NotFound(response, "Vehicle not found."); return; }
            ResponseHelper.Ok(response, vehicle);
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

        public void GetHistory(HttpListenerResponse response)
        {
            ResponseHelper.NotFound(response, "Vehicle history not implemented yet.");
        }
    }
}