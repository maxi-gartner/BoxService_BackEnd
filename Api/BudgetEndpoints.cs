using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

public static class BudgetEndpoints
{
    public static void MapBudgetEndpoints(this WebApplication app)
    {
        app.MapGet("/budgets", (int? vehicleId, int? page, int? pageSize, string? status, HttpContext context, BudgetService service, VehicleService vehicles) =>
        {
            var tenantId = context.TenantId();

            if (vehicleId is int vid)
            {
                // El vehículo ya se confirmó del tenant del caller — mismo
                // criterio que /vehicles/{id}/history.
                if (vehicles.GetById(vid, tenantId) is null) return Error(404, "Vehicle not found.");
                return Ok(service.GetByVehicleId(vid).Select(BudgetDto.FromModel).ToArray());
            }

            var (items, total) = service.GetPaged(tenantId, page ?? 1, pageSize ?? 25, status);
            return Ok(new PagedResult<BudgetDto>
            {
                Items = items.Select(i => BudgetDto.FromModel(i.Budget, i.Plate, i.Brand, i.Model)).ToList(),
                Page = page ?? 1,
                PageSize = pageSize ?? 25,
                TotalCount = total
            });
        });

        app.MapGet("/budgets/{id:int}", (int id, HttpContext context, BudgetService service) =>
        {
            var value = service.GetById(id, context.TenantId());
            return value is null ? Error(404, "Budget not found") :
                Ok(new { budget = BudgetDto.FromModel(value.Budget), details = value.Details, total = value.Total });
        });

        app.MapPost("/budgets", (BudgetCreateRequest request, HttpContext context, BudgetService service) => Write(() =>
        {
            var result = service.Create(context.TenantId(), request);
            return result.ok ? Ok(BudgetDto.FromModel(result.result!), 201) : Error(result.notFound ? 404 : 400, result.error);
        }));

        app.MapPatch("/budgets/{id:int}", (int id, BudgetStatusRequest request, HttpContext context, BudgetService service) => Write(() =>
        {
            var result = service.UpdateStatus(id, context.TenantId(), request);
            return result.ok ? Ok(result.result!) : Error(result.notFound ? 404 : 400, result.error);
        }));

        app.MapPut("/budgets/{id:int}/details", (int id, BudgetDetailsUpdateRequest request, HttpContext context, BudgetService service) => Write(() =>
        {
            var result = service.UpdateDetails(id, context.TenantId(), request);
            return result.ok
                ? Ok(new { budget = BudgetDto.FromModel(result.result!.Budget), details = result.result!.Details, total = result.result!.Total })
                : Error(result.notFound ? 404 : 400, result.error);
        }));

        app.MapPut("/budgets/{id:int}/service", (int id, AssignServiceRequest request, HttpContext context, BudgetService service) => Write(() =>
        {
            var result = service.AssignService(id, context.TenantId(), request.ServiceId);
            return result.ok ? Ok(new { message = "Budget linked to service" }) : Error(result.notFound ? 404 : 400, result.error);
        }));
    }
}
