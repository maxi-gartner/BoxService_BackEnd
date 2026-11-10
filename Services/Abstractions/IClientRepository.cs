using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Services.Abstractions;

public interface IClientRepository
{
    List<Client> GetAll(int tenantId);
    (List<Client> Items, int TotalCount) GetPaged(int tenantId, int page, int pageSize, string? search);
    Client? GetById(int id, int tenantId);
    List<(Client Client, string TenantName)> GetAllAdmin();
    List<ClientVehicle> GetVehiclesByClientId(int clientId, int tenantId);
    Client Create(int tenantId, ClientCreateRequest request);
}
