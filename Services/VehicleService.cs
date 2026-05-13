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

        public List<Vehicle> List() => _repository.GetAll();

        public Vehicle? GetById(int id) => _repository.GetById(id);

        public Vehicle? FindByPlate(string plate) => _repository.GetByPlate(plate);

        public Vehicle Create(VehicleCreateRequest request)
        {
            if (request.ClientId <= 0)
                throw new ArgumentException("client_id is required and must be greater than 0.");
            if (string.IsNullOrWhiteSpace(request.Brand))
                throw new ArgumentException("Brand is required.");
            if (string.IsNullOrWhiteSpace(request.Model))
                throw new ArgumentException("Model is required.");
            if (string.IsNullOrWhiteSpace(request.Plate))
                throw new ArgumentException("Plate is required.");

            var existing = _repository.GetByPlate(request.Plate);
            if (existing != null)
                throw new InvalidOperationException("A vehicle with that plate already exists.");

            var vehicle = new Vehicle
            {
                ClientId = request.ClientId,
                Brand    = request.Brand.Trim(),
                Model    = request.Model.Trim(),
                Year     = request.Year,
                Plate    = request.Plate.Trim().ToUpperInvariant()
            };

            return _repository.Create(vehicle);
        }
    }
}