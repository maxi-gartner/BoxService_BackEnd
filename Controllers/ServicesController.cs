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
            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";
            var parts = path.Split('/');

            if (parts.Length < 4 || !int.TryParse(parts[3], out int id))
            {
                ResponseHelper.BadRequest(response, "Invalid service ID");
                return;
            }

            var service = _service.GetById(id);

            if (service == null)
            {
                ResponseHelper.NotFound(response, "Service not found");
                return;
            }

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

            if (req == null)
            {
                ResponseHelper.BadRequest(response, "Invalid service data");
                return;
            }

            var service = new Service
            {
                Date = req.Date,
                Mileage = req.Mileage,
                ServiceType = req.ServiceType,
                Notes = req.Notes,
                VehicleId = req.VehicleId
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

        public void CreateDetail(HttpListenerRequest request, HttpListenerResponse response)
        {
            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";
            var parts = path.Split('/');

            // Esperado: /api/services/{id}/detalles
            if (parts.Length < 5 || !int.TryParse(parts[3], out int serviceId))
            {
                ResponseHelper.BadRequest(response, "Invalid service ID");
                return;
            }

            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = reader.ReadToEnd();

            ServiceDetail? detail;

            try
            {
                detail = JsonSerializer.Deserialize<ServiceDetail>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                ResponseHelper.BadRequest(response, "Invalid JSON");
                return;
            }

            if (detail == null)
            {
                ResponseHelper.BadRequest(response, "Invalid service detail data");
                return;
            }

            detail.ServiceId = serviceId;

            try
            {
                var created = _service.CreateDetail(detail);
                ResponseHelper.Created(response, created);
            }
            catch (Exception ex)
            {
                ResponseHelper.BadRequest(response, ex.Message);
            }
        }
    }
}