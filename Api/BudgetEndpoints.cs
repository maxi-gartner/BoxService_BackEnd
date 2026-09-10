using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

public static class BudgetEndpoints
{
    public static void MapBudgetEndpoints(this WebApplication app)
    {
        app.MapGet("/budgets", (BudgetService service) => Ok(service.GetAll().Select(BudgetDto.FromModel).ToArray()));
        app.MapGet("/budgets/{id:int}", (int id, BudgetService service) =>
        {
            var value = service.GetById(id);
            return value is null ? Error(404, "Budget not found") :
                Ok(new { budget = BudgetDto.FromModel(value.Budget), details = value.Details, total = value.Total });
        });
        app.MapPost("/budgets", (BudgetCreateRequest request, BudgetService service) => Write(() =>
        {
            var result = service.Create(request);
            return result.ok ? Ok(BudgetDto.FromModel(result.result!), 201) : Error(result.notFound ? 404 : 400, result.error);
        }));
        app.MapPatch("/budgets/{id:int}", (int id, BudgetStatusRequest request, BudgetService service) => Write(() =>
        {
            var result = service.UpdateStatus(id, request);
            return result.ok ? Ok(result.result!) : Error(result.notFound ? 404 : 400, result.error);
        }));
        app.MapPut("/budgets/{id:int}/service", (int id, AssignServiceRequest request, BudgetService service) => Write(() =>
        {
            var result = service.AssignService(id, request.ServiceId);
            return result.ok ? Ok(new { message = "Budget linked to service" }) : Error(result.notFound ? 404 : 400, result.error);
        }));
    }
}
