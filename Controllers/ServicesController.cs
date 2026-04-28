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
        private readonly ServicesService _servicesService;

        public ServicesController()
        {
            _servicesService = new ServicesService();
        }

        public void GetAll(HttpListenerResponse response)
        {
            var services = _servicesService.GetAll();

            ResponseHelper.Send(response, 200, new
            {
                success = true,
                data = services,
                error = (object?)null
            });
        }

        public void GetById(HttpListenerRequest request, HttpListenerResponse response)
        {
            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";
            var parts = path.Split('/');

            if (parts.Length < 4 || !int.TryParse(parts[3], out int id))
            {
                ResponseHelper.Send(response, 400, new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = 400, message = "ID de service inválido" }
                });
                return;
            }

            var service = _servicesService.GetById(id);

            if (service == null)
            {
                ResponseHelper.Send(response, 404, new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = 404, message = "Service no encontrado" }
                });
                return;
            }

            ResponseHelper.Send(response, 200, new
            {
                success = true,
                data = service,
                error = (object?)null
            });
        }

        public void Create(HttpListenerRequest request, HttpListenerResponse response)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = reader.ReadToEnd();

            Service? service;

            try
            {
                service = JsonSerializer.Deserialize<Service>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                ResponseHelper.Send(response, 400, new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = 400, message = "JSON inválido" }
                });
                return;
            }

            if (service == null)
            {
                ResponseHelper.Send(response, 400, new
                {
                    success = false,
                    data = (object?)null,
                    error = new { code = 400, message = "Datos del service inválidos" }
                });
                return;
            }

            var creado = _servicesService.Create(service);

            ResponseHelper.Send(response, 201, new
            {
                success = true,
                data = creado,
                error = (object?)null
            });
        }
    }
}