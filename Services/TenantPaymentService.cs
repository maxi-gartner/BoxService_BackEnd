using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class TenantPaymentService
    {
        private static readonly Regex PeriodFormat = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

        private readonly TenantPaymentRepository _repo;
        private readonly TenantRepository _tenants;

        public TenantPaymentService(TenantPaymentRepository repo, TenantRepository tenants)
        {
            _repo = repo;
            _tenants = tenants;
        }

        public List<TenantPayment> GetHistory(int tenantId) => _repo.GetByTenant(tenantId);

        public List<TenantPaymentStatus> GetOverview() =>
            _repo.GetOverview(DateTime.Today.ToString("yyyy-MM"));

        public TenantPayment Record(int tenantId, TenantPaymentCreateRequest request)
        {
            if (_tenants.GetById(tenantId) is null)
                throw new ArgumentException("El taller no existe.");

            if (string.IsNullOrWhiteSpace(request.Period) || !PeriodFormat.IsMatch(request.Period))
                throw new ArgumentException("El período debe tener el formato AAAA-MM.");

            if (request.Amount <= 0)
                throw new ArgumentException("El monto debe ser mayor a 0.");

            return _repo.Record(tenantId, request);
        }
    }
}
