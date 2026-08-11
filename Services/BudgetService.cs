using System;
using System.Collections.Generic;
using BoxService_BackEnd.Database;
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

            if (budget == null)
            {
                return null;
            }

            return new BudgetWithDetails
            {
                Budget = budget,
                Details = _repo.GetDetails(id)
            };
        }

        public (bool ok, bool notFound, string error, Budget? result) Create(BudgetCreateRequest req)
        {
            if (req.VehicleId <= 0)
                return (false, false, "vehicleId is required", null);

            if (req.Details == null || req.Details.Count == 0)
                return (false, false, "details is required and must have at least one item", null);

            foreach (var d in req.Details)
            {
                if (string.IsNullOrWhiteSpace(d.Type))
                    return (false, false, "Each detail requires a type", null);

                if (string.IsNullOrWhiteSpace(d.Description))
                    return (false, false, "Each detail requires a description", null);
            }

            try
            {
                var nro = _repo.GetNextNumber();
                var number = $"P-{nro:D4}";

                var budget = new Budget
                {
                    Number    = number,
                    VehicleId = req.VehicleId,
                    Notes     = req.Notes
                };

                var id = _repo.Create(budget);
                budget.BudgetId = id;
                budget.Date = DateTime.Today.ToString("yyyy-MM-dd");

                using var conn = DatabaseConnection.GetConnection();
                using var tx = conn.BeginTransaction();

                foreach (var d in req.Details)
                {
                    var detail = new BudgetDetail
                    {
                        BudgetId    = id,
                        Type        = d.Type,
                        Description = d.Description,
                        Quantity    = d.Quantity,
                        UnitPrice   = d.UnitPrice,
                        Subtotal    = d.Quantity * d.UnitPrice
                    };

                    _repo.CreateDetail(detail, conn, tx);
                }

                tx.Commit();

                return (true, false, "", budget);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating budget: {ex}");
                return (false, false, "Could not create the budget.", null);
            }
        }

        // Unifica lo que antes eran dos endpoints (PUT /status y POST /approve)
        // en una sola transición de estado: sent | rejected | approved.
        public (bool ok, bool notFound, string error, object? result) UpdateStatus(int id, BudgetStatusRequest req)
        {
            var budget = _repo.GetById(id);

            if (budget == null)
            {
                return (false, true, "Budget not found", null);
            }

            var valid = new[] { "sent", "rejected", "approved" };

            if (!Array.Exists(valid, s => s == req.Status))
            {
                return (false, false, "Invalid status. Use: sent | rejected | approved", null);
            }

            if (req.Status == "approved")
            {
                if (budget.Status == "approved")
                    return (false, false, "Budget already approved", null);

                if (budget.Status == "rejected")
                    return (false, false, "Cannot approve a rejected budget", null);

                try
                {
                    var approvedId = _repo.ApproveWithTransaction(id, _repo.GetDetails(id), budget.VehicleId);

                    return (true, false, "", new
                    {
                        budgetId = approvedId,
                        status = "approved",
                        message = "Budget approved. Service must be created from Services module."
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error approving budget {id}: {ex}");
                    return (false, false, "Could not approve the budget.", null);
                }
            }

            _repo.UpdateStatus(id, req.Status);

            return (true, false, "", new { budgetId = id, status = req.Status });
        }

        // Vincula un presupuesto aprobado con el service creado desde el módulo de Services.
        // Esto actualiza presupuestos.id_service.
        public (bool ok, bool notFound, string error) AssignService(int budgetId, int serviceId)
        {
            var budget = _repo.GetById(budgetId);

            if (budget == null)
            {
                return (false, true, "Budget not found");
            }

            if (budget.Status != "approved")
            {
                return (false, false, "Only approved budgets can be linked to a service");
            }

            if (budget.ServiceId != null)
            {
                return (false, false, "Budget already has a linked service");
            }

            try
            {
                _repo.AssignService(budgetId, serviceId);
                return (true, false, "");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error assigning service to budget {budgetId}: {ex}");
                return (false, false, "Could not link the service to the budget.");
            }
        }
    }
}
