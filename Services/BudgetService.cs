using System;
using System.Collections.Generic;
using System.Text.Json;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services
{
    public class BudgetService
    {
        private readonly BudgetRepository _repo = new();

        public List<Budget> GetAll() => _repo.GetAll();

        public BudgetWithDetails? GetById(int id)
        {
            var budget = _repo.GetById(id);
            if (budget == null) return null;
            return new BudgetWithDetails
            {
                Budget  = budget,
                Details = _repo.GetDetails(id)
            };
        }

        public (bool ok, string error, Budget? result) Create(string body)
        {
            try
            {
                var doc = JsonDocument.Parse(body).RootElement;

                if (!doc.TryGetProperty("vehicle_id", out var vProp))
                    return (false, "vehicle_id is required", null);

                var details = new List<BudgetDetail>();
                if (doc.TryGetProperty("details", out var detailsProp))
                {
                    foreach (var item in detailsProp.EnumerateArray())
                    {
                        var qty   = item.GetProperty("quantity").GetDecimal();
                        var price = item.GetProperty("unit_price").GetDecimal();
                        details.Add(new BudgetDetail
                        {
                            Type        = item.GetProperty("type").GetString() ?? "",
                            Description = item.GetProperty("description").GetString() ?? "",
                            Quantity    = qty,
                            UnitPrice   = price,
                            Subtotal    = qty * price
                        });
                    }
                }

                var lastNumber = _repo.GetLastNumber();
                var nro        = int.Parse(lastNumber.Split('-')[1]) + 1;
                var number     = $"P-{nro:D4}";

                var budget = new Budget
                {
                    Number    = number,
                    VehicleId = vProp.GetInt32(),
                    Notes     = doc.TryGetProperty("notes", out var notes) ? notes.GetString() : null
                };

                var id = _repo.Create(budget);
                budget.BudgetId = id;

                using var conn = Database.DatabaseConnection.GetConnection();
                using var tx   = conn.BeginTransaction();
                foreach (var d in details)
                {
                    d.BudgetId = id;
                    _repo.CreateDetail(d, conn, tx);
                }
                tx.Commit();

                return (true, "", budget);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public (bool ok, string error) UpdateStatus(int id, string body)
        {
            var budget = _repo.GetById(id);
            if (budget == null) return (false, "Budget not found");

            var doc    = JsonDocument.Parse(body).RootElement;
            var status = doc.GetProperty("status").GetString() ?? "";

            var valid = new[] { "sent", "rejected" };
            if (!Array.Exists(valid, s => s == status))
                return (false, "Invalid status. Use: sent | rejected");

            _repo.UpdateStatus(id, status);
            return (true, "");
        }

        public (bool ok, string error, object? result) Approve(int id)
        {
            var budget = _repo.GetById(id);
            if (budget == null)                  return (false, "Budget not found", null);
            if (budget.Status == "approved")     return (false, "Budget already approved", null);
            if (budget.Status == "rejected")     return (false, "Cannot approve a rejected budget", null);

            var details   = _repo.GetDetails(id);
            var serviceId = _repo.ApproveWithTransaction(id, details, budget.VehicleId);

            return (true, "", new
            {
                budget_id          = id,
                status             = "approved",
                service_id_created = serviceId
            });
        }
    }
}
