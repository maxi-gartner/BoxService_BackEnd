using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Storage;
using Microsoft.Extensions.Configuration;

namespace BoxService_BackEnd.UnitTests.Api;

// SQL boundary doubles, NOT an alternative implementation of the services.
// Reads honor repository contracts; writes intentionally do not authorize:
// a missing guard in the real service/handler is visible as a write and mutation.
internal sealed class TenantIsolationStore
{
    public List<Client> Clients { get; } = new();
    public List<Vehicle> Vehicles { get; } = new();
    public List<Budget> Budgets { get; } = new();
    public List<BudgetDetail> BudgetDetails { get; } = new();
    public List<Service> Services { get; } = new();
    public List<ServiceDetail> Details { get; } = new();
    public List<Invoice> Invoices { get; } = new();
    public List<ServiceInspection> Inspections { get; } = new();
    public List<ServiceInspectionPhoto> Photos { get; } = new();
    public List<string> Writes { get; } = new();
    public List<(string Resource, int Id, int Tenant)> ScopedReads { get; } = new();
    public List<int> PortalClientReads { get; } = new();
    public List<(int Service, int Photo)> PhotoReads { get; } = new();
    public int InspectionReads { get; set; }
    public int DetailReads { get; set; }
    public int StorageCalls { get; set; }
    public int AuditCalls { get; set; }

    public TenantIsolationStore()
    {
        foreach (var id in new[] { 101, 102, 201 })
        {
            var tenant = id / 100;
            Clients.Add(new Client { ClientId = id, TenantId = tenant, Name = $"PRIVATE-CLIENT-{id}", Active = true, Phone = "123" });
            Vehicles.Add(new Vehicle { VehicleId = id, TenantId = tenant, ClientId = id, Plate = $"PLATE-{id}", Brand = "Brand", Model = "Model" });
            Budgets.Add(new Budget { BudgetId = id, TenantId = tenant, VehicleId = id, Status = "approved", Number = $"P-{id}" });
            BudgetDetails.Add(new BudgetDetail { BudgetId = id, Description = $"PRIVATE-BUDGET-{id}", Type = "labor", Quantity = 1, UnitPrice = 10, Subtotal = 10 });
            Services.Add(new Service { ServiceId = id, TenantId = tenant, VehicleId = id, Notes = $"PRIVATE-SERVICE-{id}", Date = new DateTime(2026, 1, 1), Mileage = 10 });
            Details.Add(new ServiceDetail { DetailId = id, ServiceId = id, Description = $"PRIVATE-DETAIL-{id}" });
            Invoices.Add(new Invoice { InvoiceId = id, TenantId = tenant, ServiceId = id, Number = $"F-{id}", Status = "issued", Total = 10 });
            Inspections.Add(new ServiceInspection { InspectionId = id, ServiceId = id, GeneralObservations = $"PRIVATE-INSPECTION-{id}" });
            Photos.Add(new ServiceInspectionPhoto { PhotoId = id, InspectionId = id, StoragePath = $"private/{id}.jpg" });
        }
    }

    public string Snapshot() => JsonSerializer.Serialize(new { Clients, Vehicles, Budgets, BudgetDetails, Services, Details, Invoices, Inspections, Photos });
    public void Read(string resource, int id, int tenant) => ScopedReads.Add((resource, id, tenant));

    public sealed class ClientsRepo(TenantIsolationStore s) : ClientRepository
    {
        public override Client? GetById(int id, int tenant) { s.Read("clients", id, tenant); return s.Clients.SingleOrDefault(x => x.ClientId == id && x.TenantId == tenant); }
        public override Client? GetById(int id) { s.PortalClientReads.Add(id); return s.Clients.SingleOrDefault(x => x.ClientId == id); }
        public override List<Client> GetAll(int tenant) { s.Read("clients", 0, tenant); return s.Clients.Where(x => x.TenantId == tenant).ToList(); }
        public override (List<Client> Items, int TotalCount) GetPaged(int tenant, int page, int size, string? search) { var items = GetAll(tenant); return (items, items.Count); }
        public override List<ClientVehicle> GetVehiclesByClientId(int id, int tenant) { s.Read("client-vehicles", id, tenant); return s.Vehicles.Where(v => v.ClientId == id && v.TenantId == tenant).Select(v => new ClientVehicle { VehicleId = v.VehicleId }).ToList(); }
        public override Client Create(int tenant, ClientCreateRequest request) { s.Writes.Add("client-create"); var item = new Client { ClientId = 999, TenantId = tenant, Name = request.Name ?? "" }; s.Clients.Add(item); return item; }
        public override List<(Client Client, string TenantName)> GetAllAdmin() => throw new InvalidOperationException("Unexpected admin query");
    }

    public sealed class VehiclesRepo(TenantIsolationStore s) : VehicleRepository
    {
        public override Vehicle? GetById(int id, int tenant) { s.Read("vehicles", id, tenant); return s.Vehicles.SingleOrDefault(x => x.VehicleId == id && x.TenantId == tenant); }
        public override List<Vehicle> GetAll(int tenant) { s.Read("vehicles", 0, tenant); return s.Vehicles.Where(x => x.TenantId == tenant).ToList(); }
        public override (List<(Vehicle Vehicle, string? ClientName)> Items, int TotalCount) GetPaged(int tenant, int page, int size, string? search) { var rows = GetAll(tenant).Select(v => (v, (string?)"Client")).ToList(); return (rows, rows.Count); }
        public override Vehicle? GetByPlate(string plate, int tenant) { s.Read("vehicle-plate", 0, tenant); return s.Vehicles.SingleOrDefault(x => x.Plate == plate && x.TenantId == tenant); }
        public override (Vehicle Vehicle, string? ClientName)? GetByPlateAnywhere(string plate) { var v = s.Vehicles.SingleOrDefault(x => x.Plate == plate); return v is null ? null : (v, s.Clients.Single(c => c.ClientId == v.ClientId).Name); }
        public override List<Vehicle> GetByClientId(int id, int tenant) { s.Read("client-vehicles", id, tenant); return s.Vehicles.Where(x => x.ClientId == id && x.TenantId == tenant).ToList(); }
        public override List<Vehicle> GetByClientId(int id) => throw new InvalidOperationException("Unexpected unscoped vehicle lookup");
        public override bool UpdateOwner(int id, int tenant, int client) { s.Writes.Add("vehicle-owner"); s.Vehicles.Single(x => x.VehicleId == id).ClientId = client; return true; }
        public override Vehicle Create(Vehicle value) { s.Writes.Add("vehicle-create"); value.VehicleId = 999; s.Vehicles.Add(value); return value; }
    }

    public sealed class BudgetsRepo(TenantIsolationStore s) : BudgetRepository
    {
        public override Budget? GetById(int id, int tenant) { s.Read("budgets", id, tenant); return s.Budgets.SingleOrDefault(x => x.BudgetId == id && x.TenantId == tenant); }
        public override List<Budget> GetAll(int tenant) { s.Read("budgets", 0, tenant); return s.Budgets.Where(x => x.TenantId == tenant).ToList(); }
        public override (List<(Budget Budget, string Plate, string Brand, string Model)> Items, int TotalCount) GetPaged(int tenant, int page, int size, string? status, string? search = null) { var rows = GetAll(tenant).Select(x => (x, "plate", "brand", "model")).ToList(); return (rows, rows.Count); }
        public override List<Budget> GetByVehicleId(int id, int tenant) { s.Read("vehicle-budgets", id, tenant); return s.Budgets.Where(x => x.VehicleId == id && x.TenantId == tenant).ToList(); }
        public override List<BudgetDetail> GetDetails(int id) { s.DetailReads++; return s.BudgetDetails.Where(x => x.BudgetId == id).ToList(); }
        public override long GetNextNumber() => 999;
        public override int CreateWithDetails(Budget value, List<BudgetDetail> details) { s.Writes.Add("budget-create"); value.BudgetId = 999; s.Budgets.Add(value); return 999; }
        public override void ReplaceDetails(int id, List<BudgetDetail> details) { s.Writes.Add("budget-details"); s.BudgetDetails.RemoveAll(x => x.BudgetId == id); s.BudgetDetails.AddRange(details); }
        public override bool UpdateStatus(int id, int tenant, string status) { s.Writes.Add("budget-status"); s.Budgets.Single(x => x.BudgetId == id).Status = status; return true; }
        public override void AssignService(int id, int tenant, int service) { s.Writes.Add("budget-service"); s.Budgets.Single(x => x.BudgetId == id).ServiceId = service; }
    }

    public sealed class ServicesRepo(TenantIsolationStore s) : ServicesRepository
    {
        public override Service? GetByIdScoped(int id, int tenant) { s.Read("services", id, tenant); return s.Services.SingleOrDefault(x => x.ServiceId == id && x.TenantId == tenant); }
        public override Service? GetById(int id) => throw new InvalidOperationException("Unexpected unscoped service lookup");
        public override List<Service> GetAll(int tenant) { s.Read("services", 0, tenant); return s.Services.Where(x => x.TenantId == tenant).ToList(); }
        public override List<Service> GetByVehicleId(int id, int tenant) { s.Read("vehicle-services", id, tenant); return s.Services.Where(x => x.VehicleId == id && x.TenantId == tenant).ToList(); }
        public override List<ServiceDetail> GetDetailsByServiceId(int id) { s.DetailReads++; return s.Details.Where(x => x.ServiceId == id).ToList(); }
        public override Service Create(Service value) { s.Writes.Add("service-create"); value.ServiceId = 999; s.Services.Add(value); return value; }
        public override ServiceDetail CreateDetail(ServiceDetail value) { s.Writes.Add("service-detail"); s.Details.Add(value); return value; }
    }

    public sealed class InvoicesRepo(TenantIsolationStore s) : InvoiceRepository
    {
        public override Invoice? GetById(int id, int tenant) { s.Read("invoices", id, tenant); return s.Invoices.SingleOrDefault(x => x.InvoiceId == id && x.TenantId == tenant); }
        public override List<Invoice> GetAll(int tenant) { s.Read("invoices", 0, tenant); return s.Invoices.Where(x => x.TenantId == tenant).ToList(); }
        public override (List<(Invoice Invoice, int VehicleId, string Plate, string Brand, string Model)> Items, int TotalCount) GetPaged(int tenant, int page, int size, string? status, string? search = null) { var rows = GetAll(tenant).Select(x => (x, x.ServiceId, "plate", "brand", "model")).ToList(); return (rows, rows.Count); }
        public override List<Invoice> GetByVehicleId(int id, int tenant) { s.Read("vehicle-invoices", id, tenant); return s.Invoices.Where(x => x.ServiceId == id && x.TenantId == tenant).ToList(); }
        public override (int TotalCount, int PendingCount, decimal PendingTotal) GetStats(int tenant) { var rows = GetAll(tenant); return (rows.Count, rows.Count, rows.Sum(x => x.Total)); }
        public override List<InvoiceDetail> GetDetails(int id, int tenant) => GetById(id, tenant)?.Details ?? new();
        public override bool InvoiceExistsForService(int id) => s.Invoices.Any(x => x.ServiceId == id);
        public override long GetNextNumber() => 999;
        public override int CreateWithTransaction(Invoice value) { s.Writes.Add("invoice-create"); value.InvoiceId = 999; s.Invoices.Add(value); return 999; }
        public override bool UpdateStatus(int id, int tenant, string status) { s.Writes.Add("invoice-status"); s.Invoices.Single(x => x.InvoiceId == id).Status = status; return true; }
    }

    public sealed class InspectionsRepo(TenantIsolationStore s) : ServiceInspectionRepository(null!)
    {
        public override Task<bool> ServiceExistsAsync(int id, CancellationToken ct) { s.InspectionReads++; return Task.FromResult(s.Services.Any(x => x.ServiceId == id)); }
        public override Task<ServiceInspection?> GetByServiceIdAsync(int id, CancellationToken ct) { s.InspectionReads++; return Task.FromResult(s.Inspections.SingleOrDefault(x => x.ServiceId == id)); }
        public override Task<ServiceInspectionPhoto?> GetPhotoAsync(int service, int photo, CancellationToken ct) { s.PhotoReads.Add((service, photo)); return Task.FromResult(s.Photos.SingleOrDefault(p => p.PhotoId == photo && s.Inspections.Any(i => i.InspectionId == p.InspectionId && i.ServiceId == service))); }
        public override Task<ServiceInspection> CreateAsync(ServiceInspection value, CancellationToken ct) { s.Writes.Add("inspection-create"); s.Inspections.Add(value); return Task.FromResult(value); }
        public override Task<ServiceInspection?> UpdateAsync(ServiceInspection value, CancellationToken ct) { s.Writes.Add("inspection-update"); s.Inspections.RemoveAll(i => i.ServiceId == value.ServiceId); s.Inspections.Add(value); return Task.FromResult<ServiceInspection?>(value); }
        public override Task<int> CountPhotosAsync(int id, CancellationToken ct) => Task.FromResult(s.Photos.Count(x => x.InspectionId == id));
        public override Task<ServiceInspectionPhoto> CreatePhotoAsync(ServiceInspectionPhoto value, CancellationToken ct) { s.Writes.Add("photo-create"); s.Photos.Add(value); return Task.FromResult(value); }
        public override Task<bool> DeletePhotoAsync(int id, CancellationToken ct) { s.Writes.Add("photo-delete"); return Task.FromResult(s.Photos.RemoveAll(p => p.PhotoId == id) > 0); }
    }

    public sealed class Storage(TenantIsolationStore s) : SupabaseStorageService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Supabase:Url"] = "", ["Supabase:Key"] = "" }).Build())
    {
        public override Task UploadAsync(Stream stream, string path, string type, CancellationToken ct) { s.StorageCalls++; return Task.CompletedTask; }
        public override Task<string> CreateSignedUrlAsync(string path, int expiresInSeconds = 900) { s.StorageCalls++; return Task.FromResult("https://storage.invalid/" + path); }
        public override Task DeleteAsync(string path) { s.StorageCalls++; return Task.CompletedTask; }
    }

    public sealed class BranchesRepo : SucursalRepository
    {
        public override Sucursal? GetById(int id, int tenant) => null;
    }

    public sealed class AuditRepo(TenantIsolationStore s) : AuthAuditRepository
    {
        public override void Record(string eventType, string outcome, int? actorUserId, int? targetUserId, int? tenantId, string? sourceIp, string? httpMethod = null, string? resourcePath = null) => s.AuditCalls++;
    }
}
