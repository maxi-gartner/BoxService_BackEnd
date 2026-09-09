using BoxService_BackEnd.DTOs;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/catalog", (CatalogService service) => Ok(service.GetAll().Select(CatalogDto.FromModel).ToArray()));
        app.MapPost("/catalog", (CatalogItemCreateRequest request, CatalogService service) =>
            Write(() => Ok(CatalogDto.FromModel(service.Create(request)), 201)));
        app.MapPatch("/catalog/{id:int}", (int id, CatalogItemUpdateRequest request, CatalogService service) => Write(() =>
        {
            var result = service.Update(id, request);
            return result.ok ? Ok(new { message = "Catalog item updated" }) : Error(result.notFound ? 404 : 400, result.error);
        }));
        app.MapDelete("/catalog/{id:int}", (int id, CatalogService service) => Write(() =>
            service.Delete(id) ? Ok(new { message = "Catalog item deleted" }) : Error(404, "Catalog item not found")));
    }
}
