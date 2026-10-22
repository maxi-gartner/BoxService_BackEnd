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
