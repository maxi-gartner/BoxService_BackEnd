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

        public List<Invoice> GetByVehicleId(int vehicleId) => _repo.GetByVehicleId(vehicleId);

        public (int TotalCount, int PendingCount, decimal PendingTotal) GetStats(int tenantId) => _repo.GetStats(tenantId);

        public Invoice? GetById(int id, int tenantId) => _repo.GetById(id, tenantId);

        public (bool ok, bool notFound, string error, Invoice? result) Create(int tenantId, InvoiceCreateRequest req)
        {
            if (req.ServiceId <= 0)
                return (false, false, "serviceId is required", null);

            if (_servicesRepo.GetByIdScoped(req.ServiceId, tenantId) is null)
                return (false, false, "Service does not exist", null);

            if (_repo.InvoiceExistsForService(req.ServiceId))
                return (false, false, "This service already has an invoice", null);

            decimal total = 0;

            if (req.BudgetId.HasValue)
            {
                var budget = _budgetRepo.GetById(req.BudgetId.Value, tenantId);

                if (budget == null)
                    return (false, false, "budgetId does not match an existing budget", null);

                foreach (var d in _budgetRepo.GetDetails(req.BudgetId.Value))
                    total += d.Subtotal;
            }

            var nro = _repo.GetNextNumber();
            var number = $"F-{nro:D4}";

            var invoice = new Invoice
            {
                TenantId = tenantId,
                Number = number,
                Total = total,
                Status = "issued",
                ServiceId = req.ServiceId,
                BudgetId = req.BudgetId
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

            _repo.UpdateStatus(id, tenantId, req.Status);

            return (true, false, "", new { invoiceId = id, status = req.Status });
        }
    }
}
