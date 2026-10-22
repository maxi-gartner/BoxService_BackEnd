using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BoxService_BackEnd.Models;

namespace BoxService_BackEnd.Services
{
    // Catálogo de marcas/modelos/versiones de Mercado Libre Argentina —
    // mejora la precisión al cargar un vehículo, nunca la bloquea: si
    // falta configurar MercadoLibre:ClientId/ClientSecret o la API no
    // responde, cada método devuelve lista vacía y el formulario sigue
    // aceptando texto libre como siempre (ver docs/MERCADOLIBRE_CATALOG.md).
    public class MercadoLibreCatalogService
    {
        private const string CarsCategoryId = "MLA1744";
        private const string CarsDomainId = "MLA-CARS_AND_VANS";

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string? _clientId;
        private readonly string? _clientSecret;
        private readonly ILogger<MercadoLibreCatalogService> _logger;
        private readonly SemaphoreSlim _tokenLock = new(1, 1);

        private string? _accessToken;
        private DateTime _tokenExpiresAt = DateTime.MinValue;

        // La lista de marcas es pública y casi no cambia — se cachea en
        // memoria del proceso para no pegarle a ML en cada alta de vehículo.
        private List<VehicleCatalogOption>? _cachedBrands;
        private DateTime _brandsCachedAt = DateTime.MinValue;

        // Singleton (ver Program.cs) para que el cache de marcas/token
        // persista entre requests — por eso HttpClient se pide acá vía
        // IHttpClientFactory en vez de inyectarlo directo.
        public MercadoLibreCatalogService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<MercadoLibreCatalogService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _clientId = configuration["MercadoLibre:ClientId"];
            _clientSecret = configuration["MercadoLibre:ClientSecret"];
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

        // Dependen de un token de app (MercadoLibre:ClientId/ClientSecret) —
        // sin eso configurado, devuelven lista vacía (el form sigue
        // aceptando texto libre).
        public Task<List<VehicleCatalogOption>> GetModelsAsync(string brandId, CancellationToken ct = default) =>
            GetTopValuesAsync("MODEL", new[] { ("BRAND", brandId) }, ct);

        public Task<List<VehicleCatalogOption>> GetVersionsAsync(string brandId, string modelId, CancellationToken ct = default) =>
            GetTopValuesAsync("TRIM", new[] { ("BRAND", brandId), ("MODEL", modelId) }, ct);

        private async Task<List<VehicleCatalogOption>> GetTopValuesAsync(string attributeId, (string Id, string ValueId)[] knownAttributes, CancellationToken ct)
        {
            var token = await EnsureTokenAsync(ct);
            if (token is null) return new List<VehicleCatalogOption>();

            try
            {
                var body = new
                {
                    known_attributes = knownAttributes.Select(a => new { id = a.Id, value_id = a.ValueId }),
                };

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://api.mercadolibre.com/catalog_domains/{CarsDomainId}/attributes/{attributeId}/top_values")
                {
                    Content = JsonContent.Create(body),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var http = _httpClientFactory.CreateClient();
                var response = await http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) return new List<VehicleCatalogOption>();

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                var values = await JsonSerializer.DeserializeAsync<List<MlTopValue>>(stream, JsonOptions, ct);
                return (values ?? new List<MlTopValue>())
                    .Select(v => new VehicleCatalogOption { Id = v.Id, Name = v.Name })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo traer valores de {Attribute} de Mercado Libre.", attributeId);
                return new List<VehicleCatalogOption>();
            }
        }

        private async Task<string?> EnsureTokenAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(_clientSecret))
                return null;

            if (_accessToken is not null && DateTime.UtcNow < _tokenExpiresAt)
                return _accessToken;

            await _tokenLock.WaitAsync(ct);
            try
            {
                if (_accessToken is not null && DateTime.UtcNow < _tokenExpiresAt)
                    return _accessToken;

                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                });

                var http = _httpClientFactory.CreateClient();
                var response = await http.PostAsync("https://api.mercadolibre.com/oauth/token", form, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Mercado Libre rechazó el pedido de token ({Status}).", response.StatusCode);
                    return null;
                }

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                var token = await JsonSerializer.DeserializeAsync<MlTokenResponse>(stream, JsonOptions, ct);
                if (token?.AccessToken is null) return null;

                _accessToken = token.AccessToken;
                // Margen de 5 minutos antes del vencimiento real para no
                // arriesgarse a usar un token que expira a mitad de request.
                _tokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn - 300));
                return _accessToken;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo obtener el token de Mercado Libre.");
                return null;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

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

        private sealed class MlTopValue
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        private sealed class MlTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
        }
    }
}
