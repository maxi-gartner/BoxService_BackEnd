using System;
using System.Collections.Generic;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class InvoiceService
    {
        private readonly InvoiceRepository _repo = new();
        private readonly BudgetRepository _budgetRepo = new();
        private readonly ServicesRepository _servicesRepo = new();

        public List<Invoice> GetAll(int tenantId) => _repo.GetAll(tenantId);

        public (List<(Invoice Invoice, int VehicleId, string Plate, string Brand, string Model)> Items, int TotalCount) GetPaged(
            int tenantId, int page, int pageSize, string? status) =>
            _repo.GetPaged(tenantId, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), status);

        public List<Invoice> GetByVehicleId(int vehicleId, int tenantId) => _repo.GetByVehicleId(vehicleId, tenantId);

        public (int TotalCount, int PendingCount, decimal PendingTotal) GetStats(int tenantId) => _repo.GetStats(tenantId);

        public Invoice? GetById(int id, int tenantId) => _repo.GetById(id, tenantId);

        public (bool ok, bool notFound, string error, Invoice? result) Create(int tenantId, InvoiceCreateRequest req)
        {
            if (req.ServiceId <= 0)
                return (false, false, "serviceId is required", null);

            var service = _servicesRepo.GetByIdScoped(req.ServiceId, tenantId);
            if (service is null)
                return (false, false, "Service does not exist", null);

            if (_repo.InvoiceExistsForService(req.ServiceId))
                return (false, false, "This service already has an invoice", null);

            if (new VehicleRepository().GetById(service.VehicleId, tenantId) is null)
                return (false, true, "Vehículo no encontrado en este taller.", null);

            // Si el service ya tiene presupuesto, usarlo aunque el caller omita su ID.
            var budget = req.BudgetId is int budgetId
                ? _budgetRepo.GetById(budgetId, tenantId)
                : _budgetRepo.GetByVehicleId(service.VehicleId, tenantId).FirstOrDefault(b => b.ServiceId == service.ServiceId);
            if (req.BudgetId.HasValue && budget is null)
                return (false, true, "Presupuesto no encontrado en este taller.", null);
            if (budget is not null && req.Details is { Count: > 0 })
                return (false, false, "Este service tiene presupuesto: los conceptos e importes se toman de él.", null);

            // Calcular siempre en el servidor; nunca aceptar total/subtotal del navegador.
            var requests = budget is null ? req.Details : _budgetRepo.GetDetails(budget.BudgetId)
                .Select(d => new InvoiceDetailRequest { Type = d.Type, Description = d.Description,
                    Quantity = d.Quantity, UnitPrice = d.UnitPrice }).ToList();
            List<InvoiceDetail> details;
            try { details = InvoiceRules.BuildDetails(requests); }
            catch (ArgumentException error) { return (false, false, error.Message, null); }
            var total = details.Sum(d => d.Subtotal);
            var validationError = InvoiceRules.Validate(service, budget, tenantId, total);
            if (validationError is not null) return (false, false, validationError, null);

            var nro = _repo.GetNextNumber();
            var number = $"F-{nro:D4}";

            var invoice = new Invoice
            {
                TenantId = tenantId,
                Number = number,
                Total = total,
                Status = "issued",
                ServiceId = req.ServiceId,
                BudgetId = budget?.BudgetId,
                Details = details
            };

            var id = _repo.CreateWithTransaction(invoice);
            invoice.InvoiceId = id;
            invoice.Date = DateTime.Today.ToString("yyyy-MM-dd");

            return (true, false, "", invoice);
        }

        public (bool ok, bool notFound, string error, object? result) UpdateStatus(int id, int tenantId, InvoiceStatusRequest req)
        {
            var invoice = _repo.GetById(id, tenantId);

            if (invoice == null)
                return (false, true, "Invoice not found", null);

            if (invoice.Status == "cancelled")
                return (false, false, "Cannot modify a cancelled invoice", null);

            var valid = new[] { "paid", "cancelled" };

            if (!Array.Exists(valid, s => s == req.Status))
                return (false, false, "Invalid status. Use: paid | cancelled", null);

            if (!_repo.UpdateStatus(id, tenantId, req.Status))
                return (false, false, "La factura cambió de estado. Actualizá la pantalla.", null);

            return (true, false, "", new { invoiceId = id, status = req.Status });
        }
    }
}
