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
            // Validaci�n b�sica: un ID 0 o negativo no sirve.
            if (id <= 0) return null;

            // Si el ID est� bien, se consulta en la base.
            return _repository.GetById(id);
        }

        // GET /api/vehiculos/{id}/historial
        // Historial de services de un vehículo puntual.
        public List<Service> GetByVehicleId(int vehicleId)
        {
            if (vehicleId <= 0) return new List<Service>();

            return _repository.GetByVehicleId(vehicleId);
        }

        // GET /api/services/{id}/details
        public List<ServiceDetail> GetDetails(int serviceId)
        {
            if (serviceId <= 0) return new List<ServiceDetail>();

            return _repository.GetDetailsByServiceId(serviceId);
        }

        // POST /api/services
        // Crea un service nuevo.
        public Service Create(Service service)
        {
            // 1. Sanitizar: limpiar textos antes de validar/guardar.
            Sanitize(service);

            // 2. Validar: controlar que los datos obligatorios est�n bien.
            Validate(service);

            // 3. L�gica de negocio:
            // El pr�ximo service se calcula sumando 10.000 km.
            service.NextMileage = service.Mileage + 10000;

            // El pr�ximo service tambi�n se calcula sumando 6 meses a la fecha actual del service.
            service.NextDate = service.Date.AddMonths(6);

            // 4. Guardar en la base de datos usando el Repository.
            return _repository.Create(service);
        }

        // POST /api/services/{id}/details
        // Crea un detalle para un service existente.
        public ServiceDetail CreateDetail(ServiceDetail detail)
        {
            // 1. Sanitizar: limpiar la descripci�n.
            SanitizeDetail(detail);

            // 2. Validar: controlar que el detalle tenga datos correctos.
            ValidateDetail(detail);

            // 3. Guardar en la base de datos.
            return _repository.CreateDetail(detail);
        }

        // Limpieza de textos del service.
        // Esto evita guardar valores con espacios de m�s.
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
        // Si algo est� mal, se lanza una excepci�n.
        // Esa excepci�n vuelve al Controller y el Controller responde BadRequest.
        private static void Validate(Service service)
        {
            // La fecha es obligatoria.
            if (service.Date == default)
                throw new ArgumentException("Date is required.");

            // El kilometraje tiene que ser mayor a 0.
            if (service.Mileage <= 0)
                throw new ArgumentException("Mileage must be greater than 0.");

            // El tipo de service es obligatorio.
            // IsNullOrWhiteSpace controla null, vac�o o solo espacios.
            if (string.IsNullOrWhiteSpace(service.ServiceType))
                throw new ArgumentException("Service type is required.");

            // El service tiene que estar asociado a un veh�culo.
            if (service.VehicleId <= 0)
                throw new ArgumentException("Vehicle is required.");
        }

        // Validaciones principales del detalle del service.
        private static void ValidateDetail(ServiceDetail detail)
        {
            // El detalle tiene que pertenecer a un service existente.
            if (detail.ServiceId <= 0)
                throw new ArgumentException("Service is required.");

            // La descripci�n del detalle es obligatoria.
            if (string.IsNullOrWhiteSpace(detail.Description))
                throw new ArgumentException("Detail description is required.");
        }
    }
}