using System;
using System.Collections.Generic;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class VehicleService
    {
        private readonly VehicleRepository _repository;

        public VehicleService(VehicleRepository repository)
        {
            _repository = repository;
        }

        public List<Vehicle> List(int tenantId) => _repository.GetAll(tenantId);

        public Vehicle? GetById(int id, int tenantId) => _repository.GetById(id, tenantId);

        public Vehicle? FindByPlate(string plate, int tenantId) =>
            _repository.GetByPlate(plate.Trim().ToUpperInvariant(), tenantId);

        public Vehicle Create(int tenantId, VehicleCreateRequest request)
        {
            if (request.ClientId <= 0)
                throw new ArgumentException("client_id is required and must be greater than 0.");

            if (string.IsNullOrWhiteSpace(request.Brand))
                throw new ArgumentException("Brand is required.");

            if (string.IsNullOrWhiteSpace(request.Model))
                throw new ArgumentException("Model is required.");

            if (string.IsNullOrWhiteSpace(request.Plate))
                throw new ArgumentException("Plate is required.");

            if (request.CurrentMileage < 0)
                throw new ArgumentException("Current mileage cannot be negative.");

            var plate = request.Plate.Trim().ToUpperInvariant();

            // La patente es única en toda la tabla (constraint de la base,
            // de antes del multi-tenant) — el chequeo de unicidad es
            // global a propósito, no por tenant.
            if (_repository.PlateExistsAnywhere(plate))
                throw new InvalidOperationException("A vehicle with that plate already exists.");

            var vehicle = new Vehicle
            {
                ClientId = request.ClientId,
                TenantId = tenantId,
                Brand = request.Brand.Trim(),
                Model = request.Model.Trim(),
                Version = string.IsNullOrWhiteSpace(request.Version) ? null : request.Version.Trim(),
                Year = request.Year,
                Plate = plate,
                CurrentMileage = request.CurrentMileage
            };

            return _repository.Create(vehicle);
        }
    }
}
