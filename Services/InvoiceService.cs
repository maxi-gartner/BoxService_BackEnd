using System;
using System.Collections.Generic;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class InvoiceService
    {
        private readonly InvoiceRepository _repo       = new();
        private readonly BudgetRepository  _budgetRepo = new();

        public List<Invoice> GetAll() => _repo.GetAll();

        public Invoice? GetById(int id) => _repo.GetById(id);

        public (bool ok, bool notFound, string error, Invoice? result) Create(InvoiceCreateRequest req)
        {
            if (req.ServiceId <= 0)
                return (false, false, "serviceId is required", null);

            if (_repo.InvoiceExistsForService(req.ServiceId))
                return (false, false, "This service already has an invoice", null);

            decimal total = 0;

            if (req.BudgetId.HasValue)
            {
                var budget = _budgetRepo.GetById(req.BudgetId.Value);

                if (budget == null)
                    return (false, false, "budgetId does not match an existing budget", null);

                foreach (var d in _budgetRepo.GetDetails(req.BudgetId.Value))
                    total += d.Subtotal;
            }

            try
            {
                var nro    = _repo.GetNextNumber();
                var number = $"F-{nro:D4}";

                var invoice = new Invoice
                {
                    Number    = number,
                    Total     = total,
                    Status    = "issued",
                    ServiceId = req.ServiceId,
                    BudgetId  = req.BudgetId
                };

                var id = _repo.CreateWithTransaction(invoice);
                invoice.InvoiceId = id;
                invoice.Date      = DateTime.Today.ToString("yyyy-MM-dd");

                return (true, false, "", invoice);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating invoice: {ex}");
                return (false, false, "Could not create the invoice.", null);
            }
        }

        public (bool ok, bool notFound, string error, object? result) UpdateStatus(int id, InvoiceStatusRequest req)
        {
            var invoice = _repo.GetById(id);

            if (invoice == null)
                return (false, true, "Invoice not found", null);

            if (invoice.Status == "cancelled")
                return (false, false, "Cannot modify a cancelled invoice", null);

            var valid = new[] { "paid", "cancelled" };

            if (!Array.Exists(valid, s => s == req.Status))
                return (false, false, "Invalid status. Use: paid | cancelled", null);

            _repo.UpdateStatus(id, req.Status);

            return (true, false, "", new { invoiceId = id, status = req.Status });
        }
    }
}
