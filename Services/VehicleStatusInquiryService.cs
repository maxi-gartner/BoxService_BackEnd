using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services;

public class VehicleStatusInquiryService(VehicleStatusInquiryRepository repository, VehicleRepository vehicles)
{
    // (ok, rateLimited, notFound, result)
    public (bool Ok, bool RateLimited, bool NotFound, VehicleStatusInquiry? Result) Create(
        int tenantId, int vehicleId, int clientId)
    {
        var vehicle = vehicles.GetById(vehicleId, tenantId);
        if (vehicle is null || vehicle.ClientId != clientId)
            return (false, false, true, null);

        var inquiry = repository.Create(tenantId, vehicleId, clientId);
        return inquiry is null
            ? (false, true, false, null)
            : (true, false, false, inquiry);
    }

    public List<VehicleStatusInquiryRow> ListPending(int tenantId) => repository.ListPending(tenantId);

    public bool Resolve(int tenantId, int id) => repository.Resolve(tenantId, id);
}
