namespace BoxService_BackEnd.Models
{
    // Sugerencia de marca/modelo/versión para el alta de vehículos,
    // sacada del catálogo público de Mercado Libre Argentina — el
    // formulario sigue aceptando texto libre si no aparece en la lista
    // (ver docs/MERCADOLIBRE_CATALOG.md).
    public class VehicleCatalogOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
