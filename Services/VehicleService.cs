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

        public (List<(Vehicle Vehicle, string? ClientName)> Items, int TotalCount) ListPaged(
            int tenantId, int page, int pageSize, string? search) =>
            _repository.GetPaged(tenantId, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), search);

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

            if (new ClientRepository().GetById(request.ClientId, tenantId) is null)
                throw new ArgumentException("Cliente no encontrado en este taller.");

            var plate = request.Plate.Trim().ToUpperInvariant();

            // La patente es única en toda la tabla (constraint de la base,
            // de antes del multi-tenant) — el chequeo de unicidad es
            // global a propósito, no por tenant. Si el vehículo existente
            // es de ESTE taller, devolvemos quién es el dueño actual para
            // poder ofrecer una transferencia en vez de bloquear sin más
            // (caso típico: el auto cambió de dueño). Si es de otro
            // taller, seguimos bloqueando sin exponer datos ajenos.
            var existing = _repository.GetByPlateAnywhere(plate);
            if (existing is not null)
            {
                var (existingVehicle, existingClientName) = existing.Value;
                if (existingVehicle.TenantId == tenantId)
                {
                    throw new VehiclePlateConflictException(
                        existingVehicle.VehicleId,
                        existingVehicle.ClientId,
                        existingClientName ?? $"cliente #{existingVehicle.ClientId}");
                }

                throw new InvalidOperationException("A vehicle with that plate already exists.");
            }

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

        // El auto se vendió / cambió de dueño: re-apunta id_cliente sin
        // tocar el vehículo en sí, así que el historial de services,
        // presupuestos y facturas (que cuelgan de vehicleId) se conserva
        // y pasa a verse bajo el nuevo dueño.
        public Vehicle Transfer(int tenantId, int vehicleId, int newClientId)
        {
            var vehicle = _repository.GetById(vehicleId, tenantId)
                ?? throw new KeyNotFoundException("Vehicle not found.");

            if (new ClientRepository().GetById(newClientId, tenantId) is null)
                throw new ArgumentException("Cliente no encontrado en este taller.");

            if (vehicle.ClientId == newClientId)
                throw new InvalidOperationException("El vehículo ya pertenece a ese cliente.");

            _repository.UpdateOwner(vehicleId, tenantId, newClientId);
            return _repository.GetById(vehicleId, tenantId)!;
        }
    }
}
