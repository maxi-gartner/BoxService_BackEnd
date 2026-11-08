using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class TenantService
    {
        private readonly TenantRepository _tenants;
        private readonly UserRepository _users;
        private readonly PasswordHasher<UserAccount> _passwordHasher = new();

        public TenantService(TenantRepository tenants, UserRepository users)
        {
            _tenants = tenants;
            _users = users;
        }

        public List<Tenant> List() => _tenants.GetAll();

        public List<TenantWithOwner> ListWithOwner() => _tenants.GetAllWithOwner();

        public Tenant? GetById(int id) => _tenants.GetById(id);

        public (Tenant Tenant, UserAccount Owner) Create(TenantCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BusinessName))
                throw new ArgumentException("El nombre del negocio es obligatorio.");

            if (string.IsNullOrWhiteSpace(request.OwnerName))
                throw new ArgumentException("El nombre del dueño es obligatorio.");

            if (string.IsNullOrWhiteSpace(request.Username))
                throw new ArgumentException("El usuario es obligatorio.");

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                throw new ArgumentException("La contraseña debe tener al menos 6 caracteres.");

            if (_users.UsernameExists(request.Username))
                throw new ArgumentException("Ese nombre de usuario ya está en uso.");

            var passwordHash = _passwordHasher.HashPassword(new UserAccount(), request.Password);

            return _tenants.CreateWithOwner(
                request.BusinessName.Trim(),
                request.Username.Trim(),
                passwordHash,
                request.OwnerName.Trim(),
                string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim(),
                string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim());
        }

        public bool Update(int id, TenantUpdateRequest request) => _tenants.Update(id, request);
    }
}
