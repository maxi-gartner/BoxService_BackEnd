using System;
using System.Collections.Generic;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class ServicesService
    {
        // Repository = capa que habla con la base de datos.
        // El Service no hace SQL directo, le pide al Repository que guarde o consulte.
        private readonly ServicesRepository _repository = new();

        // GET /api/services
        // Trae todos los services desde la base de datos.
        public List<Service> GetAll() => _repository.GetAll();

        // GET /api/services/{id}
        // Trae un service puntual por ID.
        public Service? GetById(int id)
        {
            // Validación básica: un ID 0 o negativo no sirve.
            if (id <= 0) return null;

            // Si el ID está bien, se consulta en la base.
            return _repository.GetById(id);
        }

        // POST /api/services
        // Crea un service nuevo.
        public Service Create(Service service)
        {
            // 1. Sanitizar: limpiar textos antes de validar/guardar.
            Sanitize(service);

            // 2. Validar: controlar que los datos obligatorios estén bien.
            Validate(service);

            // 3. Lógica de negocio:
            // El próximo service se calcula sumando 10.000 km.
            service.NextMileage = service.Mileage + 10000;

            // El próximo service también se calcula sumando 6 meses a la fecha actual del service.
            service.NextDate = service.Date.AddMonths(6);

            // 4. Guardar en la base de datos usando el Repository.
            return _repository.Create(service);
        }

        // POST /api/services/{id}/details
        // Crea un detalle para un service existente.
        public ServiceDetail CreateDetail(ServiceDetail detail)
        {
            // 1. Sanitizar: limpiar la descripción.
            SanitizeDetail(detail);

            // 2. Validar: controlar que el detalle tenga datos correctos.
            ValidateDetail(detail);

            // 3. Guardar en la base de datos.
            return _repository.CreateDetail(detail);
        }

        // Limpieza de textos del service.
        // Esto evita guardar valores con espacios de más.
        private static void Sanitize(Service service)
        {
            service.ServiceType = service.ServiceType?.Trim();
            service.Notes = service.Notes?.Trim();
        }

        // Limpieza de textos del detalle.
        private static void SanitizeDetail(ServiceDetail detail)
        {
            detail.Description = detail.Description?.Trim();
        }

        // Validaciones principales del service.
        // Si algo está mal, se lanza una excepción.
        // Esa excepción vuelve al Controller y el Controller responde BadRequest.
        private static void Validate(Service service)
        {
            // La fecha es obligatoria.
            if (service.Date == default)
                throw new Exception("Date is required.");

            // El kilometraje tiene que ser mayor a 0.
            if (service.Mileage <= 0)
                throw new Exception("Mileage must be greater than 0.");

            // El tipo de service es obligatorio.
            // IsNullOrWhiteSpace controla null, vacío o solo espacios.
            if (string.IsNullOrWhiteSpace(service.ServiceType))
                throw new Exception("Service type is required.");

            // El service tiene que estar asociado a un vehículo.
            if (service.VehicleId <= 0)
                throw new Exception("Vehicle is required.");
        }

        // Validaciones principales del detalle del service.
        private static void ValidateDetail(ServiceDetail detail)
        {
            // El detalle tiene que pertenecer a un service existente.
            if (detail.ServiceId <= 0)
                throw new Exception("Service is required.");

            // La descripción del detalle es obligatoria.
            if (string.IsNullOrWhiteSpace(detail.Description))
                throw new Exception("Detail description is required.");
        }
    }
}