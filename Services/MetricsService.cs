using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class MetricsService
    {
        private readonly MetricsRepository _repo = new();

        public TenantMetrics GetMetrics(int tenantId) => _repo.GetMetrics(tenantId);
    }
}
