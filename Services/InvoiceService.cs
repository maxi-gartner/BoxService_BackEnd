using System;
using System.Collections.Generic;
using System.Text.Json;
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

        public (bool ok, string error, Invoice? result) Create(string body)
        {
            try
            {
                var doc = JsonDocument.Parse(body).RootElement;

                if (!doc.TryGetProperty("service_id", out var sProp))
                    return (false, "service_id is required", null);

                var serviceId = sProp.GetInt32();

                if (_repo.InvoiceExistsForService(serviceId))
                    return (false, "This service already has an invoice", null);

                decimal total    = 0;
                int?    budgetId = null;

                if (doc.TryGetProperty("budget_id", out var bProp))
                {
                    budgetId = bProp.GetInt32();
                    var details = _budgetRepo.GetDetails(budgetId.Value);
                    foreach (var d in details) total += d.Subtotal;
                }

                var lastNumber = _repo.GetLastNumber();
                var nro        = int.Parse(lastNumber.Split('-')[1]) + 1;
                var number     = $"F-{nro:D4}";

                var invoice = new Invoice
                {
                    Number    = number,
                    Total     = total,
                    Status    = "issued",
                    ServiceId = serviceId,
                    BudgetId  = budgetId
                };

                var id = _repo.CreateWithTransaction(invoice);
                invoice.InvoiceId = id;
                invoice.Date      = DateTime.Today.ToString("yyyy-MM-dd");

                return (true, "", invoice);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public (bool ok, string error) UpdateStatus(int id, string body)
        {
            var invoice = _repo.GetById(id);
            if (invoice == null)               return (false, "Invoice not found");
            if (invoice.Status == "cancelled") return (false, "Cannot modify a cancelled invoice");

            var doc    = JsonDocument.Parse(body).RootElement;
            var status = doc.GetProperty("status").GetString() ?? "";

            var valid = new[] { "paid", "cancelled" };
            if (!Array.Exists(valid, s => s == status))
                return (false, "Invalid status. Use: paid | cancelled");

            _repo.UpdateStatus(id, status);
            return (true, "");
        }
    }
}
