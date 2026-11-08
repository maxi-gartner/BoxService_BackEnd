using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Services
{
    // Catálogo de marca/modelo/versión al cargar un vehículo, sacado de la
    // API de ArgAutos (https://argautos.com) — mejora la precisión, nunca
    // la bloquea: si la API no responde o el rate limit anónimo (3
    // requests/min, ver docs/ARGAUTOS_CATALOG.md) se agota, devuelve la
    // última lista cacheada o vacía, y el formulario sigue aceptando texto
    // libre como siempre.
    //
    // Sin API key por ahora (decisión explícita, no pendiente): el cacheo
    // agresivo de acá de abajo está pensado justamente para vivir dentro
    // del límite anónimo. Si en uso real no alcanza, ArgAutos vende planes
    // (Starter/Pro) — agregar la key es sumar un header Authorization, no
    // un cambio de arquitectura.
    public class ArgAutosCatalogService
    {
        private const string BaseUrl = "https://argautos.com/api/v1";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ArgAutosCatalogService> _logger;

        private List<VehicleCatalogOption>? _cachedBrands;
        private DateTime _brandsCachedAt = DateTime.MinValue;

        private readonly ConcurrentDictionary<string, (List<VehicleCatalogOption> Options, DateTime CachedAt)> _modelsByBrand = new();
        private readonly ConcurrentDictionary<string, (List<VehicleCatalogOption> Options, DateTime CachedAt)> _versionsByModel = new();

        // Singleton (ver Program.cs) para que los caches de abajo
        // persistan entre requests — por eso HttpClient se pide vía
        // IHttpClientFactory en vez de inyectarlo directo.
        public ArgAutosCatalogService(IHttpClientFactory httpClientFactory, ILogger<ArgAutosCatalogService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<List<VehicleCatalogOption>> GetBrandsAsync(CancellationToken ct = default)
        {
            if (_cachedBrands is not null && DateTime.UtcNow - _brandsCachedAt < CacheTtl)
                return _cachedBrands;

            var result = await FetchAllPagesAsync("/argautos/brands", ct);
            if (result is null) return _cachedBrands ?? new List<VehicleCatalogOption>();

            _cachedBrands = result;
            _brandsCachedAt = DateTime.UtcNow;
            return result;
        }

        public async Task<List<VehicleCatalogOption>> GetModelsAsync(string brandId, CancellationToken ct = default)
        {
            if (_modelsByBrand.TryGetValue(brandId, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheTtl)
                return cached.Options;

            var result = await FetchAllPagesAsync($"/argautos/brands/{brandId}/models", ct);
            if (result is null) return cached.Options ?? new List<VehicleCatalogOption>();

            _modelsByBrand[brandId] = (result, DateTime.UtcNow);
            return result;
        }

        // brandId no hace falta para este endpoint (el modelo ya identifica
        // la marca) — se mantiene en la firma para que coincida con la ruta
        // /vehicle-catalog/versions?brandId=X&modelId=Y del lado del cliente.
        public async Task<List<VehicleCatalogOption>> GetVersionsAsync(string brandId, string modelId, CancellationToken ct = default)
        {
            if (_versionsByModel.TryGetValue(modelId, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheTtl)
                return cached.Options;

            var result = await FetchAllPagesAsync($"/argautos/models/{modelId}/versions", ct);
            if (result is null) return cached.Options ?? new List<VehicleCatalogOption>();

            _versionsByModel[modelId] = (result, DateTime.UtcNow);
            return result;
        }

        // Todas las listas del catálogo ArgAutos usan la misma forma de
        // página ({id, name, ...} + meta.current_page/last_page), así que
        // un solo fetcher paginado sirve para marcas, modelos y versiones.
        // per_page=100 (el máximo que acepta la API) para que, en la
        // inmensa mayoría de los casos, alcance una sola página.
        private async Task<List<VehicleCatalogOption>?> FetchAllPagesAsync(string path, CancellationToken ct)
        {
            const int MaxPages = 10;
            var options = new List<VehicleCatalogOption>();

            try
            {
                var http = _httpClientFactory.CreateClient();
                var page = 1;
                while (true)
                {
                    var response = await http.GetAsync($"{BaseUrl}{path}?per_page=100&page={page}", ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("ArgAutos devolvió {Status} para {Path} (página {Page}).", (int)response.StatusCode, path, page);
                        return options.Count > 0 ? options : null;
                    }

                    using var stream = await response.Content.ReadAsStreamAsync(ct);
                    var parsed = await JsonSerializer.DeserializeAsync<ArgAutosPage<ArgAutosItem>>(stream, JsonOptions, ct);
                    if (parsed?.Data is null) break;

                    options.AddRange(parsed.Data
                        .Where(i => i.Name is not null)
                        .Select(i => new VehicleCatalogOption { Id = IdToString(i.Id), Name = i.Name! }));

                    if (parsed.Meta is null || parsed.Meta.CurrentPage >= parsed.Meta.LastPage || page >= MaxPages)
                        break;

                    page++;
                }

                return options
                    .OrderBy(o => o.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo traer el catálogo de ArgAutos ({Path}).", path);
                return options.Count > 0 ? options : null;
            }
        }

        private static string IdToString(JsonElement id) => id.ValueKind switch
        {
            JsonValueKind.Number => id.GetRawText(),
            JsonValueKind.String => id.GetString() ?? string.Empty,
            _ => string.Empty,
        };

        private sealed class ArgAutosPage<T>
        {
            [JsonPropertyName("data")]
            public List<T>? Data { get; set; }

            [JsonPropertyName("meta")]
            public ArgAutosMeta? Meta { get; set; }
        }

        private sealed class ArgAutosMeta
        {
            [JsonPropertyName("current_page")]
            public int CurrentPage { get; set; }

            [JsonPropertyName("last_page")]
            public int LastPage { get; set; }
        }

        // Misma forma para marca ({id, name, models_count}), modelo
        // ({id, brand_id, name, versions_count}) y versión ({id, name,
        // available_years}) — solo id/name importan para las sugerencias.
        private sealed class ArgAutosItem
        {
            [JsonPropertyName("id")]
            public JsonElement Id { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }
    }
}
