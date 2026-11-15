using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using BoxService_BackEnd.Auth;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Services.Abstractions;
using BoxService_BackEnd.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoxService_BackEnd.UnitTests.Api;

[Collection("Security regression")]
public sealed class SecurityConfigurationTests(SecurityRegressionFactory factory)
{
    // Reserved test domains only: never used as deployment defaults.
    private const string TestOrigin = "https://frontend.test";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("*")]
    [InlineData("https://*.frontend.test")]
    [InlineData("frontend.test")]
    [InlineData("//frontend.test")]
    [InlineData("ftp://frontend.test")]
    [InlineData("https://user:password@frontend.test")]
    [InlineData("https://frontend.test/path")]
    [InlineData("https://frontend.test?x=1")]
    [InlineData("https://frontend.test#fragment")]
    [InlineData("https://frontend.test https://another.test")]
    [InlineData("https://frontend.test,https://another.test")]
    public void Production_RejectsMissingOrInvalidOrigin(string? origin)
    {
        using var host = Host("Production", origin);
        var error = Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        Assert.Contains("Cors:AllowedOrigin", error.Message);
        Assert.DoesNotContain("password@", error.Message);
    }

    [Theory]
    [InlineData("https://frontend.test", "https://frontend.test")]
    [InlineData("https://frontend.test/", "https://frontend.test")]
    [InlineData("http://localhost:3000", "http://localhost:3000")]
    [InlineData("https://frontend.test:8443", "https://frontend.test:8443")]
    public async Task Production_UsesExplicitOrigin(string configured, string origin)
    {
        using var host = Host("Production", configured);
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Development_WithoutOrigin_KeepsWildcard(string? configured)
    {
        using var host = Host("Development", configured);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", TestOrigin);
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Production", false)]
    [InlineData("Development", true)]
    [InlineData("Development", false)]
    public async Task Preflight_OnlyAllowsConfiguredOrigin(string environment, bool allowed)
    {
        using var host = Host(environment, TestOrigin);
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/clients");
        request.Headers.Add("Origin", allowed ? TestOrigin : "https://untrusted.test");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed)
        {
            Assert.Equal(TestOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Contains("POST", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")));
            Assert.Contains("authorization", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")));
        }
        AssertHeaders(response, sensitive: true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoginLimits_AreIndependent_AndRecoveryKeepsItsSharedLimit(bool portalFirst)
    {
        using var host = Host();
        using var client = host.CreateClient();
        var paths = portalFirst ? new[] { "/portal/auth/google", "/auth/login" } : new[] { "/auth/login", "/portal/auth/google" };
        foreach (var path in paths)
        {
            for (var i = 0; i < 10; i++)
            {
                using var response = await Malformed(client, path);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                AssertHeaders(response, true);
            }
            using var rejected = await Malformed(client, path);
            await Assert429(rejected);
        }
        for (var i = 0; i < 5; i++)
        {
            using var response = await Malformed(client, i % 2 == 0 ? "/auth/forgot-password" : "/auth/reset-password");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var recoveryRejected = await Malformed(client, "/auth/reset-password");
        await Assert429(recoveryRejected);
    }

    [Fact]
    public async Task PortalLimit_PartitionsByConnectionIp_AndIgnoresUntrustedForwardedHeader()
    {
        using var host = Host();
        using var startupClient = host.CreateClient();
        for (var i = 0; i < 10; i++) Assert.Equal(400, await PortalRequest("192.0.2.1", "198.51.100." + (i + 1)));
        Assert.Equal(429, await PortalRequest("192.0.2.1", "198.51.100.99"));
        Assert.Equal(400, await PortalRequest("192.0.2.2", "198.51.100.99"));

        async Task<int> PortalRequest(string ip, string forwarded)
        {
            using var body = new MemoryStream(Encoding.UTF8.GetBytes("["));
            var context = await host.Server.SendAsync(http =>
            {
                http.Connection.RemoteIpAddress = IPAddress.Parse(ip);
                http.Request.Method = "POST";
                http.Request.Path = "/portal/auth/google";
                http.Request.ContentType = "application/json";
                http.Request.ContentLength = 1;
                http.Request.Body = body;
                http.Request.Headers["X-Forwarded-For"] = forwarded;
            });
            return context.Response.StatusCode;
        }
    }

    [Theory]
    [InlineData("/", null, 200, false)]
    [InlineData("/clients", null, 401, true)]
    [InlineData("/tenants", "empleado", 403, true)]
    [InlineData("/clients/999", "dueno", 404, true)]
    [InlineData("/clients/101", "dueno", 200, true)]
    [InlineData("/services/101/inspection/photos/101/url", "dueno", 200, true)]
    [InlineData("/portal/me", "customer", 200, true)]
    public async Task Headers_CoverPrivateResponsesAndErrors_WithoutDisablingPublicCache(string path, string? role, int status, bool sensitive)
    {
        using var host = Host();
        using var client = host.CreateClient();
        if (role is not null)
        {
            using var authenticated = factory.Client(role, 1, role == "customer" ? 101 : 1001);
            client.DefaultRequestHeaders.Authorization = authenticated.DefaultRequestHeaders.Authorization;
        }
        using var response = await client.GetAsync(path);
        Assert.Equal(status, (int)response.StatusCode);
        AssertHeaders(response, sensitive);
    }

    [Fact]
    public async Task Headers_SurviveGlobalExceptionHandlerClearingResponse()
    {
        using var host = Host();
        // A simulated repository failure, without any DB call.
        using var failingHost = host.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<ClientRepository>(new FailingClients())));
        using var client = failingHost.CreateClient();
        using var authenticated = factory.Client("dueno", 1);
        client.DefaultRequestHeaders.Authorization = authenticated.DefaultRequestHeaders.Authorization;
        using var response = await client.GetAsync("/clients/101");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertHeaders(response, true);
        Assert.DoesNotContain("simulated failure", await response.Content.ReadAsStringAsync());
    }

    private sealed class FailingClients : ClientRepository
    {
        public override BoxService_BackEnd.Models.Client? GetById(int id, int tenantId) => throw new Exception("simulated failure");
    }

    private WebApplicationFactory<AuthService> Host(string environment = "Testing", string? origin = null)
    {
        var state = new TenantIsolationStore();
        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Cors:AllowedOrigin", origin ?? "");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ClientRepository>(new TenantIsolationStore.ClientsRepo(state));
                services.AddSingleton<VehicleRepository>(new TenantIsolationStore.VehiclesRepo(state));
                services.AddSingleton<BudgetRepository>(new TenantIsolationStore.BudgetsRepo(state));
                services.AddSingleton<ServicesRepository>(new TenantIsolationStore.ServicesRepo(state));
                services.AddSingleton<ServiceInspectionRepository>(new TenantIsolationStore.InspectionsRepo(state));
                services.AddSingleton<SupabaseStorageService>(new TenantIsolationStore.Storage(state));
                services.AddSingleton<AuthAuditRepository>(new TenantIsolationStore.AuditRepo(state));
            });
        });
    }

    private static async Task<HttpResponseMessage> Malformed(HttpClient client, string path)
    {
        using var body = new StringContent("[", Encoding.UTF8, "application/json");
        return await client.PostAsync(path, body);
    }

    private static void AssertHeaders(HttpResponseMessage response, bool sensitive)
    {
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        if (sensitive) Assert.True(response.Headers.CacheControl?.NoStore == true);
        else Assert.Null(response.Headers.CacheControl);
    }

    private static async Task Assert429(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(429, json.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        AssertHeaders(response, true);
    }
}
