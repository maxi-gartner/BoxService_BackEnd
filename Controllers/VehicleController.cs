using System;
using System.Net;
using System.Threading.Tasks;
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

        public async Task GetAllAsync(HttpListenerContext context)
        {
            var vehicles = await _service.ListAsync();
            ResponseHelper.WriteResponse(context.Response, 200, vehicles);
        }

        public async Task GetByIdAsync(HttpListenerContext context, int id)
        {
            var vehicle = await _service.GetByIdAsync(id);
            if (vehicle is null)
            {
                ResponseHelper.WriteError(context.Response, 404, "Vehicle not found.");
                return;
            }
            ResponseHelper.WriteResponse(context.Response, 200, vehicle);
        }

        public async Task SearchByPlateAsync(HttpListenerContext context)
        {
            var plate = context.Request.QueryString["plate"];
            if (string.IsNullOrWhiteSpace(plate))
            {
                ResponseHelper.WriteError(context.Response, 400, "Query param 'plate' is required.");
                return;
            }

            var vehicle = await _service.FindByPlateAsync(plate.Trim());
            if (vehicle is null)
            {
                ResponseHelper.WriteError(context.Response, 404, "Vehicle not found.");
                return;
            }
            ResponseHelper.WriteResponse(context.Response, 200, vehicle);
        }

        public async Task CreateAsync(HttpListenerContext context)
        {
            var request = await ResponseHelper.ReadJsonBodyAsync<VehicleCreateRequest>(context.Request);
            if (request is null)
            {
                ResponseHelper.WriteError(context.Response, 400, "Invalid or empty JSON body.");
                return;
            }

            try
            {
                var created = await _service.CreateAsync(request);
                ResponseHelper.WriteResponse(context.Response, 201, created);
            }
            catch (ArgumentException ex)
            {
                ResponseHelper.WriteError(context.Response, 400, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                ResponseHelper.WriteError(context.Response, 409, ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating vehicle: {ex}");
                ResponseHelper.WriteError(context.Response, 500, "Internal error creating vehicle.");
            }
        }

        public Task GetVehicleHistoryAsync(HttpListenerContext context)
        {
            ResponseHelper.WriteError(context.Response, 501, "Vehicle history not implemented yet.");
            return Task.CompletedTask;
        }
    }
}