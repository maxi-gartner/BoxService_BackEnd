using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services;

public class WorkOrderService(WorkOrderRepository repository, VehicleRepository vehicles)
{
    public (bool Ok, bool NotFound, bool AlreadyOpen, WorkOrder? Result) Receive(
        int tenantId, int vehicleId, string? notes, string receivedBy)
    {
        if (vehicles.GetById(vehicleId, tenantId) is null)
            return (false, true, false, null);

        var order = repository.Create(tenantId, vehicleId, notes, receivedBy);
        return order is null ? (false, false, true, null) : (true, false, false, order);
    }

    public WorkOrder? GetLatestForVehicle(int vehicleId, int tenantId) =>
        repository.GetLatestForVehicle(vehicleId, tenantId);

    public List<WorkOrderRow> ListOpen(int tenantId) => repository.ListOpen(tenantId);

    public (bool Ok, bool NotFound, bool InvalidTransition) Advance(
        int tenantId, int id, string targetStatus, string? notes, string? deliveredBy)
    {
        var order = repository.GetById(id, tenantId);
        if (order is null) return (false, true, false);

        if (!WorkOrderTransitions.IsValid(order.Status, targetStatus))
            return (false, false, true);

        // Compare-and-swap en el repo: si perdemos la carrera (alguien
        // más ya lo avanzó entre el GetById y acá), 0 filas — mismo
        // resultado que una transición inválida para quien llamó.
        var updated = repository.UpdateStatus(id, tenantId, order.Status, targetStatus, notes, deliveredBy);
        return updated ? (true, false, false) : (false, false, true);
    }

    public (bool Ok, bool NotFound, bool InvalidState) Cancel(int tenantId, int id)
    {
        var order = repository.GetById(id, tenantId);
        if (order is null) return (false, true, false);
        if (order.Status != "en_espera") return (false, false, true);

        return (repository.Cancel(id, tenantId), false, false);
    }
}
