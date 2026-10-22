using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class UserService
    {
        private static readonly string[] AllowedCreatableRoles = ["dueno", "empleado"];

        private readonly UserRepository _repository;
        private readonly PasswordHasher<UserAccount> _passwordHasher = new();

        public UserService(UserRepository repository)
        {
            _repository = repository;
        }

        public List<UserAccount> ListByTenant(int tenantId) => _repository.GetByTenant(tenantId);

        public List<UserWithTenantName> ListAll() =>
            _repository.GetAllWithTenant()
                .Select(row => new UserWithTenantName(row.User, row.TenantName))
                .ToList();

        // dueno dando de alta un empleado de su propio taller — el rol
        // queda fijo, no se confía en lo que mande el cliente.
        public UserAccount CreateEmployee(int tenantId, UserCreateRequest request) =>
            CreateInternal(tenantId, request, "empleado");

        // superadmin dando de alta un usuario en un tenant puntual — acá
        // sí puede elegir el rol, pero nunca "superadmin" por esta vía.
        public UserAccount CreateForTenant(int tenantId, UserCreateRequest request)
        {
            var role = request.Role?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(role) || !AllowedCreatableRoles.Contains(role))
                throw new ArgumentException("El rol debe ser 'dueno' o 'empleado'.");

            return CreateInternal(tenantId, request, role);
        }

        public UserAccount? Update(int id, int? callerTenantId, UserUpdateRequest request)
        {
            var updated = _repository.Update(id, callerTenantId, request);
            return updated ? _repository.GetById(id) : null;
        }

        private UserAccount CreateInternal(int tenantId, UserCreateRequest request, string role)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
                throw new ArgumentException("El usuario es obligatorio.");

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                throw new ArgumentException("La contraseña debe tener al menos 6 caracteres.");

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("El nombre es obligatorio.");

            if (_repository.UsernameExists(request.Username))
                throw new ArgumentException("Ese nombre de usuario ya está en uso.");

            var passwordHash = _passwordHasher.HashPassword(new UserAccount(), request.Password);

            return _repository.Create(
                tenantId,
                request.Username.Trim(),
                passwordHash,
                request.Name.Trim(),
                request.Email,
                role);
        }
    }

    public sealed record UserWithTenantName(UserAccount User, string? TenantName);
}
