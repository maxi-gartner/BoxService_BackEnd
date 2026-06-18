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

        public List<Client> List() => _repository.GetAll();

        public Client? GetById(int id) => _repository.GetById(id);

        public List<ClientVehicle>? GetVehicles(int clientId)
        {
            var client = _repository.GetById(clientId);
            if (client is null)
                return null;

            return _repository.GetVehiclesByClientId(clientId);
        }

        public Client Create(ClientCreateRequest request)
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

            return _repository.Create(request);
        }
    }
}
