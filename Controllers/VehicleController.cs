using System;
using System.Net;
using System.Threading.Tasks;
using BoxService_BackEnd.Services; 
using BoxService_BackEnd.Models;

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
                ResponseHelper.WriteError(context.Response, 404, "Vehículo no encontrado.");
                return;
            }
            ResponseHelper.WriteResponse(context.Response, 200, vehicle);
        }

        public async Task SearchByPlateAsync(HttpListenerContext context)
        {
            var plate = context.Request.QueryString["patente"];
            if (string.IsNullOrWhiteSpace(plate))
            {
                ResponseHelper.WriteError(context.Response, 400, "El parámetro 'patente' es obligatorio.");
                return;
            }

            var vehicle = await _service.FindByPlateAsync(plate.Trim());
            if (vehicle is null)
            {
                ResponseHelper.WriteError(context.Response, 404, "Vehículo no encontrado.");
                return;
            }
            ResponseHelper.WriteResponse(context.Response, 200, vehicle);
        }

        public async Task CreateAsync(HttpListenerContext context)
        {
            var request = await ResponseHelper.ReadJsonBodyAsync<VehicleCreateRequest>(context.Request);
            if (request is null)
            {
                ResponseHelper.WriteError(context.Response, 400, "Body JSON inválido o vacío.");
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
                Console.WriteLine($"Error creando vehículo: {ex}");
                ResponseHelper.WriteError(context.Response, 500, "Error interno al crear el vehículo.");
            }
        }

        public Task GetVehicleHistoryAsync(HttpListenerContext context)
        {
            ResponseHelper.WriteError(context.Response, 501, "Historial de vehículo aún no implementado.");
            return Task.CompletedTask;
        }
    }
}