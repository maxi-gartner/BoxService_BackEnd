using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this WebApplication app)
    {
        app.MapGet("/invoices", (HttpContext context, InvoiceService service) =>
            Ok(service.GetAll(context.TenantId()).Select(InvoiceDto.FromModel).ToArray()));

        app.MapGet("/invoices/{id:int}", (int id, HttpContext context, InvoiceService service) =>
        {
            var value = service.GetById(id, context.TenantId());
            return value is null ? Error(404, "Invoice not found") : Ok(InvoiceDto.FromModel(value));
        });

        app.MapPost("/invoices", (InvoiceCreateRequest request, HttpContext context, InvoiceService service) => Write(() =>
        {
            var result = service.Create(context.TenantId(), request);
            return result.ok ? Ok(InvoiceDto.FromModel(result.result!), 201) : Error(result.notFound ? 404 : 400, result.error);
        }));

        app.MapPatch("/invoices/{id:int}", (int id, InvoiceStatusRequest request, HttpContext context, InvoiceService service) => Write(() =>
        {
            var result = service.UpdateStatus(id, context.TenantId(), request);
            return result.ok ? Ok(result.result!) : Error(result.notFound ? 404 : 400, result.error);
        }));
    }
}
