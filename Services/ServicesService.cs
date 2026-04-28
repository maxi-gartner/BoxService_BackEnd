using System;
using System.Collections.Generic;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    /// <summary>
    /// Capa de lógica de negocio para Services.
    /// 
    /// Se encarga de:
    /// - Validar los datos antes de guardar
    /// - Calcular el próximo kilometraje del service
    /// - Calcular la próxima fecha del service
    /// - Llamar al Repository para acceder a la base de datos
    /// </summary>
    public class ServicesService
    {
        private readonly ServicesRepository _repository;

        public ServicesService()
        {
            _repository = new ServicesRepository();
        }

        public List<Service> GetAll()
        {
            return _repository.GetAll();
        }

        public Service? GetById(int id)
        {
            if (id <= 0)
            {
                return null;
            }

            return _repository.GetById(id);
        }

        public Service Create(Service service)
        {
            ValidarService(service);

            service.ProximoKm = service.Kilometraje + 10000;
            service.ProximaFecha = service.Fecha.AddMonths(6);

            return _repository.Create(service);
        }

        private void ValidarService(Service service)
        {
            if (service.Fecha == default)
            {
                throw new Exception("La fecha del service es obligatoria.");
            }

            if (service.Kilometraje <= 0)
            {
                throw new Exception("El kilometraje debe ser mayor a 0.");
            }

            if (string.IsNullOrWhiteSpace(service.TipoService))
            {
                throw new Exception("El tipo de service es obligatorio.");
            }

            if (service.IdVehiculo <= 0)
            {
                throw new Exception("El vehículo es obligatorio.");
            }
        }
    }
}