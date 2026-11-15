using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BoxService_BackEnd.Api;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Services;
using BoxService_BackEnd.Services.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BoxService_BackEnd.UnitTests.Api;

[Collection("Security regression")]
public sealed class PhotoErrorRegressionTests(SecurityRegressionFactory productionFactory)
{
    private const string GenericMessage = "El almacenamiento de fotografías no está disponible. Intentá nuevamente más tarde.";
    private const string InternalMessage = "Supabase Storage no está configurado. Configurá Supabase:Url y Supabase:Key en appsettings.json.";
    private const string SecretMessage = "Password=SECRET_SENTINEL; Authorization: Bearer TOKEN_SENTINEL; https://storage.invalid/photo?token=SIGNED_SENTINEL";

    [Theory]
    [InlineData("POST", false)]
    [InlineData("GET", false)]
    [InlineData("DELETE", false)]
    [InlineData("POST", true)]
    [InlineData("GET", true)]
    [InlineData("DELETE", true)]
    public async Task StorageFailure_ReturnsGeneric503_AndLogsOnlySafeDiagnostics(string method, bool secret)
    {
        var fake = new FakePhotos { Failure = new InvalidOperationException(secret ? SecretMessage : InternalMessage,
            new Exception("INNER_SECRET_SENTINEL")) };
        var logs = new CapturedLogs();
        await using var app = await StartAsync(fake, logs);
        using var client = app.GetTestClient();
        using var response = await SendAsync(client, method);
        await AssertErrorAsync(response, 503, GenericMessage);
        Assert.Equal(1, fake.Calls);

        var entry = Assert.Single(logs.Entries, e => e.EventId.Id == 50301);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Null(entry.Exception); // Exception.ToString() can contain credentials and signed URLs.
        var fields = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(entry.State).ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal(7, fields["ServiceId"]);
        Assert.Equal(method == "POST" ? "upload" : method == "GET" ? "signed-url" : "delete", fields["Operation"]);
        Assert.Equal(typeof(InvalidOperationException).FullName, fields["ExceptionType"]);
        Assert.False(string.IsNullOrWhiteSpace(fields["TraceId"]?.ToString()));
        if (method != "POST") Assert.Equal(9, fields["PhotoId"]);
        foreach (var log in logs.Entries)
        {
            var rendered = log.Message + log.Exception;
            Assert.DoesNotContain("SENTINEL", rendered);
            Assert.DoesNotContain("appsettings.json", rendered);
            Assert.DoesNotContain("Supabase:Key", rendered);
        }
    }

    [Theory]
    [InlineData("POST", 201)]
    [InlineData("GET", 200)]
    [InlineData("DELETE", 200)]
    public async Task SuccessfulPhotoOperations_KeepTheirResponse(string method, int status)
    {
        var fake = new FakePhotos();
        await using var app = await StartAsync(fake, new CapturedLogs());
        using var client = app.GetTestClient();
        using var response = await SendAsync(client, method);
        Assert.Equal(status, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        var data = json.RootElement.GetProperty("data");
        if (method == "POST") Assert.Equal(9, data.GetProperty("photoId").GetInt32());
        if (method == "GET")
        {
            Assert.Equal("https://storage.invalid/signed", data.GetProperty("url").GetString());
            Assert.Equal(900, data.GetProperty("expiresIn").GetInt32());
        }
        if (method == "DELETE") Assert.True(data.GetProperty("deleted").GetBoolean());
        Assert.Equal(1, fake.Calls);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task MissingPhoto_Keeps404(string method)
    {
        await using var app = await StartAsync(new FakePhotos { Missing = true }, new CapturedLogs());
        using var client = app.GetTestClient();
        using var response = await SendAsync(client, method);
        await AssertErrorAsync(response, 404, "No se encontró la foto.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadDomainErrors_KeepTheirPublicMessages(bool missingInspection)
    {
        var message = missingInspection ? "No se encontró la inspección." : "La foto no puede superar los 8 MB.";
        var failure = missingInspection ? (Exception)new KeyNotFoundException(message) : new ArgumentException(message);
        await using var app = await StartAsync(new FakePhotos { Failure = failure }, new CapturedLogs());
        using var client = app.GetTestClient();
        using var response = await SendAsync(client, "POST");
        await AssertErrorAsync(response, missingInspection ? 404 : 400, message);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task OtherTenant_NeverCallsPhotoService(string method)
    {
        var fake = new FakePhotos();
        await using var app = await StartAsync(fake, new CapturedLogs(), allowed: false);
        using var client = app.GetTestClient();
        using var response = await SendAsync(client, method);
        await AssertErrorAsync(response, 404, "Service no encontrado.");
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task UnexpectedFailure_RealGlobalHandlerReturnsGeneric500()
    {
        // GET bypasses mutation audit, and both data dependencies are simulated.
        // Uses Program.cs itself, rather than duplicating its exception handler.
        using var host = productionFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IInspectionPhotoService>(new FakePhotos { Failure = new Exception("internal-storage-diagnostic") });
            services.AddSingleton<ITenantServiceAccess>(new FakeAccess(true));
        }));
        using var authenticated = productionFactory.Client("dueno");
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = authenticated.DefaultRequestHeaders.Authorization;
        using var response = await SendAsync(client, "GET");
        await AssertErrorAsync(response, 500, "Error interno del servidor");
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, int status, string message)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").ValueKind);
        Assert.Equal(status, json.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal(message, json.RootElement.GetProperty("error").GetProperty("message").GetString());
    }

    private static async Task<WebApplication> StartAsync(FakePhotos photos, CapturedLogs logs, bool allowed = true)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        builder.Services.AddSingleton<IInspectionPhotoService>(photos);
        builder.Services.AddSingleton<ITenantServiceAccess>(new FakeAccess(allowed));
        // Declares the untouched inspection handlers as DI-bound; any accidental
        // invocation fails immediately, with no repository/storage construction.
        builder.Services.AddScoped<ServiceInspectionService>(_ => throw new InvalidOperationException("Unexpected inspection dependency"));
        var app = builder.Build();
        app.UseRouting();
        app.Use((context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("role", "dueno"), new Claim("tenantId", "3") }, "test", "name", "role"));
            return next(context);
        });
        app.UseAuthorization();
        app.MapServiceInspectionEndpoints();
        await app.StartAsync();
        return app;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method)
    {
        var path = "/services/7/inspection/photos" + (method == "POST" ? "" : method == "GET" ? "/9/url" : "/9");
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(new byte[] { 1, 2, 3 });
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(file, "file", "photo.jpg");
            form.Add(new StringContent("front"), "photoType");
            request.Content = form;
        }
        return await client.SendAsync(request);
    }

    private sealed class FakeAccess(bool allowed) : ITenantServiceAccess
    {
        public bool Exists(int serviceId, int tenantId) => allowed;
    }

    private sealed class FakePhotos : IInspectionPhotoService
    {
        public Exception? Failure { get; init; }
        public bool Missing { get; init; }
        public int Calls { get; private set; }
        private void Invoke() { Calls++; if (Failure is not null) throw Failure; }
        public Task<ServiceInspectionPhoto> AddPhotoAsync(int serviceId, IFormFile file, string type, string? description, CancellationToken cancellationToken)
        {
            Invoke();
            return Task.FromResult(new ServiceInspectionPhoto { PhotoId = 9, InspectionId = 2 });
        }
        public Task<string?> CreatePhotoUrlAsync(int serviceId, int photoId, CancellationToken cancellationToken)
        {
            Invoke();
            return Task.FromResult(Missing ? null : "https://storage.invalid/signed");
        }
        public Task<bool> DeletePhotoAsync(int serviceId, int photoId, CancellationToken cancellationToken)
        {
            Invoke();
            return Task.FromResult(!Missing);
        }
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, object? State, string Message, Exception? Exception);
    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Entries);
        public void Dispose() { }
    }
    private sealed class CaptureLogger(ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Enqueue(new LogEntry(level, eventId, state, formatter(state, exception), exception));
    }
}
