using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this WebApplication app)
    {
        app.MapGet("/invoices", (int? vehicleId, int? page, int? pageSize, string? status, HttpContext context, InvoiceService service, VehicleService vehicles) =>
        {
            var tenantId = context.TenantId();

            if (vehicleId is int vid)
            {
                if (vehicles.GetById(vid, tenantId) is null) return Error(404, "Vehicle not found.");
                return Ok(service.GetByVehicleId(vid).Select(InvoiceDto.FromModel).ToArray());
            }

            var (items, total) = service.GetPaged(tenantId, page ?? 1, pageSize ?? 25, status);
            return Ok(new PagedResult<InvoiceDto>
            {
                Items = items.Select(i => InvoiceDto.FromModel(i.Invoice, i.VehicleId, i.Plate, i.Brand, i.Model)).ToList(),
                Page = page ?? 1,
                PageSize = pageSize ?? 25,
                TotalCount = total
            });
        });

        app.MapGet("/invoices/stats", (HttpContext context, InvoiceService service) =>
        {
            var (total, pending, pendingTotal) = service.GetStats(context.TenantId());
            return Ok(new { totalCount = total, pendingCount = pending, pendingTotal });
        });

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
