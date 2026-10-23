using BoxService_BackEnd.Services;
using static BoxService_BackEnd.Api.ModuleResults;

namespace BoxService_BackEnd.Api;

// Sugerencias de marca/modelo/versión al cargar un vehículo, sacadas del
// catálogo de ArgAutos (ver docs/ARGAUTOS_CATALOG.md). No es información
// de un tenant puntual — cualquier staff autenticado (dueño o empleado)
// puede pedirlas.
public static class VehicleCatalogEndpoints
{
    public static void MapVehicleCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/vehicle-catalog/brands", async (ArgAutosCatalogService service) =>
            Ok(await service.GetBrandsAsync())).RequireAuthorization();

        app.MapGet("/vehicle-catalog/models", async (string brandId, ArgAutosCatalogService service) =>
            Ok(await service.GetModelsAsync(brandId))).RequireAuthorization();

        app.MapGet("/vehicle-catalog/versions", async (string brandId, string modelId, ArgAutosCatalogService service) =>
            Ok(await service.GetVersionsAsync(brandId, modelId))).RequireAuthorization();
    }
}
