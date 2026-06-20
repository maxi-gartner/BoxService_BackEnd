using System;
using System.IO;
using System.Net;
using System.Text.Json;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;

namespace BoxService_BackEnd.Controllers
{
    public class ClientController
    {
        private readonly ClientService _service;

        public ClientController(ClientService service)
        {
            _service = service;
        }

        public void GetAll(HttpListenerResponse response)
        {
            try
            {
                ResponseHelper.Ok(response, _service.List());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing clients: {ex}");
                ResponseHelper.InternalError(response);
            }
        }

        public void GetById(HttpListenerResponse response, int id)
        {
            try
            {
                var client = _service.GetById(id);
                if (client is null)
                {
                    ResponseHelper.NotFound(response, "Client not found.");
                    return;
                }

                ResponseHelper.Ok(response, client);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting client: {ex}");
                ResponseHelper.InternalError(response);
            }
        }

        public void GetVehicles(HttpListenerResponse response, int id)
        {
            try
            {
                var vehicles = _service.GetVehicles(id);
                if (vehicles is null)
                {
                    ResponseHelper.NotFound(response, "Client not found.");
                    return;
                }

                ResponseHelper.Ok(response, vehicles);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting client vehicles: {ex}");
                ResponseHelper.InternalError(response);
            }
        }

        public void Create(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream))
                body = reader.ReadToEnd();

            ClientCreateRequest? createRequest;
            try
            {
                createRequest = JsonSerializer.Deserialize<ClientCreateRequest>(
                    body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                ResponseHelper.BadRequest(response, "Invalid or empty JSON body.");
                return;
            }

            if (createRequest is null)
            {
                ResponseHelper.BadRequest(response, "Invalid JSON body.");
                return;
            }

            try
            {
                var created = _service.Create(createRequest);
                ResponseHelper.Created(response, created);
            }
            catch (ArgumentException ex)
            {
                ResponseHelper.BadRequest(response, ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating client: {ex}");
                ResponseHelper.InternalError(response);
            }
        }
    }
}
