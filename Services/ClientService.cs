using System;
using System.Collections.Generic;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class ClientService
    {
        private readonly ClientRepository _repository;

        public ClientService(ClientRepository repository)
        {
            _repository = repository;
        }

        public List<Client> List(int tenantId) => _repository.GetAll(tenantId);

        // Tope más alto que el resto (200, no 100): el picker de cliente
        // al cargar un vehículo pide la lista completa en un combo, no
        // una página de tabla — ver web/app/(dashboard)/vehiculos/page.tsx.
        public (List<Client> Items, int TotalCount) ListPaged(int tenantId, int page, int pageSize, string? search) =>
            _repository.GetPaged(tenantId, Math.Max(1, page), Math.Clamp(pageSize, 1, 200), search);

        public Client? GetById(int id, int tenantId) => _repository.GetById(id, tenantId);

        public List<(Client Client, string TenantName)> ListAllAdmin() => _repository.GetAllAdmin();

        public List<ClientVehicle>? GetVehicles(int clientId, int tenantId)
        {
            var client = _repository.GetById(clientId, tenantId);
            if (client is null)
                return null;

            return _repository.GetVehiclesByClientId(clientId, tenantId);
        }

        public Client Create(int tenantId, ClientCreateRequest request)
        {
            var name = request.Nombre ?? request.Name;
            var email = request.Email;

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.");

            if (!string.IsNullOrWhiteSpace(email))
            {
                var trimmedEmail = email.Trim();
                if (!trimmedEmail.Contains('@') || trimmedEmail.StartsWith('@') || trimmedEmail.EndsWith('@'))
                    throw new ArgumentException("Email is invalid.");
            }

            return _repository.Create(tenantId, request);
        }
    }
}
