using System;
using System.Collections.Generic;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class SucursalService
    {
        private readonly SucursalRepository _repository;

        public SucursalService(SucursalRepository repository)
        {
            _repository = repository;
        }

        public List<Sucursal> List(int tenantId) => _repository.GetByTenant(tenantId);

        public Sucursal Create(int tenantId, SucursalCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("El nombre de la sucursal es obligatorio.");

            return _repository.Create(tenantId, request);
        }

        public Sucursal? Update(int id, int tenantId, SucursalUpdateRequest request)
        {
            var updated = _repository.Update(id, tenantId, request);
            return updated ? _repository.GetById(id, tenantId) : null;
        }
    }
}
