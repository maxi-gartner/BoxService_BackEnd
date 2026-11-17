using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services;

public class VehicleStatusInquiryService(
    VehicleStatusInquiryRepository repository, VehicleRepository vehicles, WorkOrderRepository workOrders)
{
    // (ok, rateLimited, notFound, notReceived, result)
    public (bool Ok, bool RateLimited, bool NotFound, bool NotReceived, VehicleStatusInquiry? Result) Create(
        int tenantId, int vehicleId, int clientId)
    {
        var vehicle = vehicles.GetById(vehicleId, tenantId);
        if (vehicle is null || vehicle.ClientId != clientId)
            return (false, false, true, false, null);

        // No tiene sentido preguntar "¿cómo va?" de un auto que nunca se
        // recibió (o que ya se entregó) — no hay nada que el taller pueda
        // responder todavía. La orden más reciente ES la abierta mientras
        // no esté "entregado" (ver WorkOrderRepository: una sola abierta
        // por vehículo a la vez).
        var latestOrder = workOrders.GetLatestForVehicle(vehicleId, tenantId);
        if (latestOrder is null || latestOrder.Status == "entregado")
            return (false, false, false, true, null);

        var inquiry = repository.Create(tenantId, vehicleId, clientId);
        return inquiry is null
            ? (false, true, false, false, null)
            : (true, false, false, false, inquiry);
    }

    public List<VehicleStatusInquiryRow> ListPending(int tenantId) => repository.ListPending(tenantId);

    public bool Resolve(int tenantId, int id) => repository.Resolve(tenantId, id);
}
