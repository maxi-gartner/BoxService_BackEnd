using System;
using System.Collections.Generic;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class ServicesService
    {
        private readonly ServicesRepository _repository = new();

        public List<Service> GetAll() => _repository.GetAll();

        public Service? GetById(int id)
        {
            if (id <= 0) return null;
            return _repository.GetById(id);
        }

        public Service Create(Service service)
        {
            Validate(service);

            service.NextMileage = service.Mileage + 10000;
            service.NextDate = service.Date.AddMonths(6);

            return _repository.Create(service);
        }

        public ServiceDetail CreateDetail(ServiceDetail detail)
        {
            ValidateDetail(detail);

            return _repository.CreateDetail(detail);
        }

        private static void Validate(Service service)
        {
            if (service.Date == default)
                throw new Exception("Date is required.");

            if (service.Mileage <= 0)
                throw new Exception("Mileage must be greater than 0.");

            if (string.IsNullOrWhiteSpace(service.ServiceType))
                throw new Exception("Service type is required.");

            if (service.VehicleId <= 0)
                throw new Exception("Vehicle is required.");
        }

        private static void ValidateDetail(ServiceDetail detail)
        {
            if (detail.ServiceId <= 0)
                throw new Exception("Service is required.");

            if (string.IsNullOrWhiteSpace(detail.Description))
                throw new Exception("Detail description is required.");
        }
    }
}