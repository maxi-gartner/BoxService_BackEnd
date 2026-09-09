using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this WebApplication app)
    {
        app.MapGet("/invoices", (InvoiceService service) => Ok(service.GetAll().Select(InvoiceDto.FromModel).ToArray()));
        app.MapGet("/invoices/{id:int}", (int id, InvoiceService service) =>
        {
            var value = service.GetById(id);
            return value is null ? Error(404, "Invoice not found") : Ok(InvoiceDto.FromModel(value));
        });
        app.MapPost("/invoices", (InvoiceCreateRequest request, InvoiceService service) => Write(() =>
        {
            var result = service.Create(request);
            return result.ok ? Ok(InvoiceDto.FromModel(result.result!), 201) : Error(result.notFound ? 404 : 400, result.error);
        }));
        app.MapPatch("/invoices/{id:int}", (int id, InvoiceStatusRequest request, InvoiceService service) => Write(() =>
        {
            var result = service.UpdateStatus(id, request);
            return result.ok ? Ok(result.result!) : Error(result.notFound ? 404 : 400, result.error);
        }));
    }
}
