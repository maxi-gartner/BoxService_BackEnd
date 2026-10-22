using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.DTOs;

public sealed record CatalogDto(int CatalogId, string TenantId, string Name, string Type, decimal Price)
{
    public static CatalogDto FromModel(CatalogItem value) => new(value.CatalogId,
        value.TenantId.ToString(), value.Name, value.Type, value.Price);
}
