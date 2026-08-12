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
        // El Controller usa el Service para aplicar l�gica de negocio.
        // El Controller NO guarda en base directo.
        private readonly ServicesService _service = new();

        // GET /api/services
        // Trae todos los services.
        public void GetAll(HttpListenerResponse response)
        {
            // Llama a la capa Services.
            // ServicesService despu�s llama al Repository.
            var services = _service.GetAll();

            // Devuelve respuesta OK con la lista de services.
            ResponseHelper.Ok(response, services);
        }

        // GET /api/services/{id}
        // Trae un service espec�fico por ID.
        public void GetById(HttpListenerRequest request, HttpListenerResponse response)
        {
            // Obtiene la ruta de la URL.
            // Ejemplo: /api/services/5
            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";

            // Divide la URL por partes usando "/".
            // Ejemplo:
            // /api/services/5
            // parts[0] = ""
            // parts[1] = "api"
            // parts[2] = "services"
            // parts[3] = "5"
            var parts = path.Split('/');

            // Controla que exista el ID y que sea un n�mero v�lido.
            if (parts.Length < 4 || !int.TryParse(parts[3], out int id))
            {
                ResponseHelper.BadRequest(response, "Invalid service ID");
                return;
            }

            // Si el ID est� bien, se lo pasa al Service.
            var service = _service.GetById(id);

            // Si no encontr� el service, devuelve 404.
            if (service == null)
            {
                ResponseHelper.NotFound(response, "Service not found");
                return;
            }

            // Si lo encontr�, devuelve OK con el service.
            ResponseHelper.Ok(response, service);
        }

        // POST /api/services
        // Crea un nuevo service.
        public void Create(HttpListenerRequest request, HttpListenerResponse response)
        {
            // Lee el body que lleg� en la petici�n.
            // En un POST, los datos vienen como JSON en el body.
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = reader.ReadToEnd();

            // Este objeto representa los datos que vinieron desde el JSON.
            ServiceCreateRequest? req;

            try
            {
                // Convierte el JSON recibido en un objeto C#.
                // PropertyNameCaseInsensitive permite leer propiedades aunque vengan
                // con may�sculas o min�sculas distintas.
                req = JsonSerializer.Deserialize<ServiceCreateRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                // Si el JSON est� mal escrito o no se puede convertir, devuelve error.
                ResponseHelper.BadRequest(response, "Invalid JSON");
                return;
            }

            // Si el JSON no trajo datos v�lidos, devuelve error.
            if (req == null)
            {
                ResponseHelper.BadRequest(response, "Invalid service data");
                return;
            }

            // Arma el modelo Service con los datos recibidos.
            // Todav�a no se calcula NextMileage ni NextDate ac�.
            // Eso lo hace ServicesService porque es l�gica de negocio.
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
                // Manda el service a la capa Services.
                // Ah� se sanitiza, valida, calcula pr�ximo KM / pr�xima fecha
                // y luego se guarda usando el Repository.
                var created = _service.Create(service);

                // Devuelve 201 Created con el service creado.
                ResponseHelper.Created(response, created);
            }
            catch (ArgumentException ex)
            {
                // Error de validaci�n de negocio -> 400.
                ResponseHelper.BadRequest(response, ex.Message);
            }
            catch (Exception ex)
            {
                // Cualquier otra falla (ej. base de datos) -> 500, sin exponer el detalle interno.
                Console.WriteLine($"Error creating service: {ex}");
                ResponseHelper.InternalError(response);
            }
        }

        // GET /api/services/{id}/details
        // Trae el detalle de trabajo cargado para un service (para el ticket imprimible).
        public void GetDetails(HttpListenerResponse response, int serviceId)
        {
            var detalles = _service.GetDetails(serviceId);
            ResponseHelper.Ok(response, detalles);
        }

        // POST /api/services/{id}/details
        // Crea un detalle para un service existente.
        public void CreateDetail(HttpListenerRequest request, HttpListenerResponse response)
        {
            // Obtiene la ruta de la URL.
            // Ejemplo: /api/services/5/details
            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";

            // Divide la URL por partes.
            // Ejemplo:
            // /api/services/5/details
            // parts[0] = ""
            // parts[1] = "api"
            // parts[2] = "services"
            // parts[3] = "5"
            // parts[4] = "details"
            var parts = path.Split('/');

            // Esperado: /api/services/{id}/details
            // Controla que exista el ID del service y que sea num�rico.
            if (parts.Length < 5 || !int.TryParse(parts[3], out int serviceId))
            {
                ResponseHelper.BadRequest(response, "Invalid service ID");
                return;
            }

            // Lee el body del request.
            // En este caso, el body trae el detalle del service en formato JSON.
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = reader.ReadToEnd();

            // Objeto que va a representar el detalle recibido.
            ServiceDetail? detail;

            try
            {
                // Convierte el JSON recibido en un objeto ServiceDetail.
                detail = JsonSerializer.Deserialize<ServiceDetail>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                // Si el JSON viene mal formado, devuelve error.
                ResponseHelper.BadRequest(response, "Invalid JSON");
                return;
            }

            // Si no se pudo obtener un detalle v�lido, devuelve error.
            if (detail == null)
            {
                ResponseHelper.BadRequest(response, "Invalid service detail data");
                return;
            }

            // Asocia el detalle con el service de la URL.
            // El frontend manda la descripci�n, pero el ID del service sale de la URL.
            detail.ServiceId = serviceId;

            try
            {
                // Manda el detalle a ServicesService.
                // Ah� se sanitiza, valida y luego se guarda en detalle_service.
                var created = _service.CreateDetail(detail);

                // Devuelve 201 Created con el detalle creado.
                ResponseHelper.Created(response, created);
            }
            catch (ArgumentException ex)
            {
                // Error de validaci�n de negocio -> 400.
                ResponseHelper.BadRequest(response, ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating service detail: {ex}");
                ResponseHelper.InternalError(response);
            }
        }
    }
}