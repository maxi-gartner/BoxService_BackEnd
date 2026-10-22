using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/catalog", (HttpContext context, CatalogService service) =>
            Ok(service.GetAll(context.TenantId()).Select(CatalogDto.FromModel).ToArray()));

        app.MapPost("/catalog", (CatalogItemCreateRequest request, HttpContext context, CatalogService service) =>
            Write(() => Ok(CatalogDto.FromModel(service.Create(context.TenantId(), request)), 201)));

        app.MapPatch("/catalog/{id:int}", (int id, CatalogItemUpdateRequest request, HttpContext context, CatalogService service) => Write(() =>
        {
            var result = service.Update(id, context.TenantId(), request);
            return result.ok ? Ok(new { message = "Catalog item updated" }) : Error(result.notFound ? 404 : 400, result.error);
        }));

        app.MapDelete("/catalog/{id:int}", (int id, HttpContext context, CatalogService service) => Write(() =>
            service.Delete(id, context.TenantId()) ? Ok(new { message = "Catalog item deleted" }) : Error(404, "Catalog item not found")));
    }
}
