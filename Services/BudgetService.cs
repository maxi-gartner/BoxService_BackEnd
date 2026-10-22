using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;

namespace BoxService_BackEnd.Services;

public class BudgetService
{
    private readonly BudgetRepository _repo = new();
    private readonly ServicesRepository _services = new();

    public List<Budget> GetAll(int tenantId) => _repo.GetAll(tenantId);

    public BudgetWithDetails? GetById(int id, int tenantId)
    {
        var budget = _repo.GetById(id, tenantId);
        return budget is null ? null : new BudgetWithDetails { Budget = budget, Details = _repo.GetDetails(id) };
    }

    public (bool ok, bool notFound, string error, Budget? result) Create(int tenantId, BudgetCreateRequest req)
    {
        if (req.VehicleId <= 0) return (false, false, "vehicleId is required", null);
        if (req.Details is null || req.Details.Count == 0)
            return (false, false, "details is required and must have at least one item", null);
        foreach (var detail in req.Details)
        {
            if (detail is null || detail.Type is not ("labor" or "part"))
                return (false, false, "Each detail requires type labor or part", null);
            if (string.IsNullOrWhiteSpace(detail.Description))
                return (false, false, "Each detail requires a description", null);
            if (detail.Quantity <= 0 || detail.UnitPrice < 0)
                return (false, false, "Quantity must be positive and unitPrice non-negative", null);
        }
        var budget = new Budget
        {
            TenantId = tenantId,
            Number = $"P-{_repo.GetNextNumber():D4}",
            VehicleId = req.VehicleId,
            Notes = req.Notes,
            Date = DateTime.Today.ToString("yyyy-MM-dd")
        };
        var details = req.Details.Select(d => new BudgetDetail
        {
            Type = d.Type,
            Description = d.Description.Trim(),
            Quantity = d.Quantity,
            UnitPrice = d.UnitPrice,
            Subtotal = d.Quantity * d.UnitPrice
        }).ToList();
        budget.BudgetId = _repo.CreateWithDetails(budget, details);
        return (true, false, "", budget);
    }

    // Edita los ítems de un presupuesto que todavía no está finalizado —
    // mientras espera aprobación (draft/sent) o ya aprobado pero sin
    // service generado todavía (se puede sumar/sacar algo de último
    // momento sin tener que cancelar y armar un presupuesto nuevo).
    public (bool ok, bool notFound, string error, BudgetWithDetails? result) UpdateDetails(int id, int tenantId, BudgetDetailsUpdateRequest req)
    {
        var budget = _repo.GetById(id, tenantId);
        if (budget is null) return (false, true, "Budget not found", null);

        if (budget.Status is "completed" or "rejected")
            return (false, false, "No se puede editar un presupuesto finalizado.", null);

        if (req.Details is null || req.Details.Count == 0)
            return (false, false, "details is required and must have at least one item", null);
        foreach (var detail in req.Details)
        {
            if (detail is null || detail.Type is not ("labor" or "part"))
                return (false, false, "Each detail requires type labor or part", null);
            if (string.IsNullOrWhiteSpace(detail.Description))
                return (false, false, "Each detail requires a description", null);
            if (detail.Quantity <= 0 || detail.UnitPrice < 0)
                return (false, false, "Quantity must be positive and unitPrice non-negative", null);
        }

        var details = req.Details.Select(d => new BudgetDetail
        {
            Type = d.Type,
            Description = d.Description.Trim(),
            Quantity = d.Quantity,
            UnitPrice = d.UnitPrice,
            Subtotal = d.Quantity * d.UnitPrice
        }).ToList();

        _repo.ReplaceDetails(id, details);

        return (true, false, "", new BudgetWithDetails { Budget = budget, Details = _repo.GetDetails(id) });
    }

    public (bool ok, bool notFound, string error, object? result) UpdateStatus(int id, int tenantId, BudgetStatusRequest req)
    {
        var budget = _repo.GetById(id, tenantId);
        if (budget is null) return (false, true, "Budget not found", null);
        if (req.Status is not ("sent" or "rejected" or "approved"))
            return (false, false, "Invalid status. Use: sent | rejected | approved", null);
        if (req.Status == "approved")
        {
            if (budget.Status is "approved" or "completed")
                return (false, false, "Budget already approved", null);
            if (budget.Status == "rejected")
                return (false, false, "Cannot approve a rejected budget", null);
        }
        _repo.UpdateStatus(id, tenantId, req.Status);
        return (true, false, "", new { budgetId = id, status = req.Status });
    }

    public (bool ok, bool notFound, string error) AssignService(int budgetId, int tenantId, int serviceId)
    {
        if (serviceId <= 0) return (false, false, "Invalid service ID");
        var budget = _repo.GetById(budgetId, tenantId);
        if (budget is null) return (false, true, "Budget not found");
        if (budget.Status != "approved") return (false, false, "Only approved budgets can be linked to a service");
        if (budget.ServiceId is not null) return (false, false, "Budget already has a linked service");
        var service = _services.GetById(serviceId);
        if (service is null || service.VehicleId != budget.VehicleId)
            return (false, false, "Service must exist and belong to the budget vehicle");
        _repo.AssignService(budgetId, tenantId, serviceId);
        return (true, false, "");
    }
}
