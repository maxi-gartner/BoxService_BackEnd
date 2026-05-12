using System;
using System.IO;
using System.Net;
using System.Text.Json;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;

namespace BoxService_BackEnd.Controllers
{
    public class ServicesController
    {
        private readonly ServicesService _service = new();

        public void GetAll(HttpListenerResponse response)
        {
            var services = _service.GetAll();
            ResponseHelper.Ok(response, services);
        }

        public void GetById(HttpListenerRequest request, HttpListenerResponse response)
        {
            var path  = request.Url?.AbsolutePath.TrimEnd('/') ?? "";
            var parts = path.Split('/');

            if (parts.Length < 4 || !int.TryParse(parts[3], out int id))
            {
                ResponseHelper.BadRequest(response, "Invalid service ID");
                return;
            }

            var service = _service.GetById(id);
            if (service == null) { ResponseHelper.NotFound(response, "Service not found"); return; }

            ResponseHelper.Ok(response, service);
        }

        public void Create(HttpListenerRequest request, HttpListenerResponse response)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = reader.ReadToEnd();

            ServiceCreateRequest? req;
            try
            {
                req = JsonSerializer.Deserialize<ServiceCreateRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                ResponseHelper.BadRequest(response, "Invalid JSON");
                return;
            }

            if (req == null) { ResponseHelper.BadRequest(response, "Invalid service data"); return; }

            // Mapear request a modelo
            var service = new Service
            {
                Date        = req.Date,
                Mileage     = req.Mileage,
                ServiceType = req.ServiceType,
                Notes       = req.Notes,
                VehicleId   = req.VehicleId,
                BudgetId    = req.BudgetId
            };

            try
            {
                var created = _service.Create(service);
                ResponseHelper.Created(response, created);
            }
            catch (Exception ex)
            {
                ResponseHelper.BadRequest(response, ex.Message);
            }
        }
    }
}
