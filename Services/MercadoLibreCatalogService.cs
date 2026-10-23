using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Services
{
    // Catálogo de marcas de Mercado Libre Argentina — mejora la
    // precisión al cargar un vehículo, nunca la bloquea: si la API no
    // responde, devuelve lista vacía y el formulario sigue aceptando
    // texto libre como siempre (ver docs/MERCADOLIBRE_CATALOG.md).
    //
    // Modelo y versión quedaron afuera: requieren catalog_domains, que
    // devuelve 403 (PA_UNAUTHORIZED_RESULT_FROM_POLICIES) sin importar
    // los permisos configurados en una app estándar de ML — se probó
    // con authorization_code + PKCE + el máximo de permisos que da el
    // panel de developers.mercadolibre.com.ar, y sigue bloqueado. Queda
    // documentado por si en el futuro ML habilita el endpoint.
    public class MercadoLibreCatalogService
    {
        private const string CarsCategoryId = "MLA1744";

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MercadoLibreCatalogService> _logger;

        // La lista de marcas es pública y casi no cambia — se cachea en
        // memoria del proceso para no pegarle a ML en cada alta de vehículo.
        private List<VehicleCatalogOption>? _cachedBrands;
        private DateTime _brandsCachedAt = DateTime.MinValue;

        // Singleton (ver Program.cs) para que el cache de marcas
        // persista entre requests — por eso HttpClient se pide acá vía
        // IHttpClientFactory en vez de inyectarlo directo.
        public MercadoLibreCatalogService(IHttpClientFactory httpClientFactory, ILogger<MercadoLibreCatalogService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // Pública, sin token — funciona siempre que ML esté arriba.
        public async Task<List<VehicleCatalogOption>> GetBrandsAsync(CancellationToken ct = default)
        {
            if (_cachedBrands is not null && DateTime.UtcNow - _brandsCachedAt < TimeSpan.FromHours(24))
                return _cachedBrands;

            try
            {
                var http = _httpClientFactory.CreateClient();
                var response = await http.GetAsync($"https://api.mercadolibre.com/categories/{CarsCategoryId}/attributes", ct);
                if (!response.IsSuccessStatusCode) return _cachedBrands ?? new List<VehicleCatalogOption>();

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                var attributes = await JsonSerializer.DeserializeAsync<List<MlAttribute>>(stream, JsonOptions, ct);
                var brand = attributes?.FirstOrDefault(a => a.Id == "BRAND");
                var result = (brand?.Values ?? new List<MlValue>())
                    .Select(v => new VehicleCatalogOption { Id = v.Id, Name = v.Name })
                    .OrderBy(o => o.Name)
                    .ToList();

                _cachedBrands = result;
                _brandsCachedAt = DateTime.UtcNow;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo traer el listado de marcas de Mercado Libre.");
                return _cachedBrands ?? new List<VehicleCatalogOption>();
            }
        }

        // Ver comentario de la clase — catalog_domains no es accesible
        // con una app estándar, así que esto siempre devuelve vacío. El
        // formulario ya está hecho para convivir con eso (texto libre).
        public Task<List<VehicleCatalogOption>> GetModelsAsync(string brandId, CancellationToken ct = default) =>
            Task.FromResult(new List<VehicleCatalogOption>());

        public Task<List<VehicleCatalogOption>> GetVersionsAsync(string brandId, string modelId, CancellationToken ct = default) =>
            Task.FromResult(new List<VehicleCatalogOption>());

        private sealed class MlAttribute
        {
            public string Id { get; set; } = string.Empty;
            public List<MlValue>? Values { get; set; }
        }

        private sealed class MlValue
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }
    }
}
