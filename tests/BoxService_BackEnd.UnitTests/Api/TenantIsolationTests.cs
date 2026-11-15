using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BoxService_BackEnd.Auth;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoxService_BackEnd.UnitTests.Api;

[Collection("Security regression")]
public sealed class TenantIsolationTests(SecurityRegressionFactory factory)
{
    private const string Items = "[{\"type\":\"labor\",\"description\":\"Work\",\"quantity\":1,\"unitPrice\":10}]";

    public static IEnumerable<object[]> CrossTenantCases()
    {
        foreach (var tenant in new[] { 1, 2 })
        foreach (var role in new[] { "dueno", "empleado" })
        {
            var own = tenant * 100 + 1;
            var other = (3 - tenant) * 100 + 1;
            var rows = new (string Method, string Path, string? Body, int Status)[]
            {
                ("GET", $"/clients/{other}", null, 404),
                ("GET", $"/clients/{other}/vehicles", null, 404),
                ("GET", $"/vehicles/{other}", null, 404),
                ("GET", $"/vehicles/{other}/history", null, 404),
                ("GET", $"/vehicles?plate=PLATE-{other}", null, 404),
                ("GET", $"/budgets/{other}", null, 404),
                ("GET", $"/budgets?vehicleId={other}", null, 404),
                ("GET", $"/services/{other}", null, 404),
                ("GET", $"/services/{other}/details", null, 404),
                ("GET", $"/services?vehicleId={other}", null, 404),
                ("GET", $"/invoices/{other}", null, 404),
                ("GET", $"/invoices?vehicleId={other}", null, 404),
                ("POST", "/vehicles", $"{{\"clientId\":{other},\"brand\":\"B\",\"model\":\"M\",\"plate\":\"NEW\"}}", 400),
                ("POST", $"/vehicles/{other}/transfer", $"{{\"newClientId\":{own}}}", 404),
                ("POST", $"/vehicles/{own}/transfer", $"{{\"newClientId\":{other}}}", 400),
                ("POST", "/budgets", $"{{\"vehicleId\":{other},\"details\":{Items}}}", 404),
                ("PATCH", $"/budgets/{other}", "{\"status\":\"sent\"}", 404),
                ("PUT", $"/budgets/{other}/details", $"{{\"details\":{Items}}}", 404),
                ("PUT", $"/budgets/{other}/service", $"{{\"serviceId\":{own}}}", 404),
                ("PUT", $"/budgets/{own}/service", $"{{\"serviceId\":{other}}}", 400),
                ("POST", "/services", $"{{\"vehicleId\":{other},\"date\":\"2026-01-01\",\"mileage\":10,\"serviceType\":\"Oil\"}}", 400),
                ("POST", $"/services/{other}/details", "{\"description\":\"change\",\"done\":true}", 404),
                ("POST", "/invoices", $"{{\"serviceId\":{other},\"details\":{Items}}}", 400),
                ("POST", "/invoices", $"{{\"serviceId\":{own},\"budgetId\":{other}}}", 404),
                ("PATCH", $"/invoices/{other}", "{\"status\":\"paid\"}", 404),
                ("POST", $"/clients/{other}/portal-invite", "{}", 404),
                ("GET", $"/services/{other}/inspection", null, 404),
                ("POST", $"/services/{other}/inspection", "{}", 404),
                ("PATCH", $"/services/{other}/inspection", "{}", 404),
                ("POST", $"/services/{other}/inspection/photos", "multipart", 404),
                ("GET", $"/services/{other}/inspection/photos/{other}/url", null, 404),
                ("DELETE", $"/services/{other}/inspection/photos/{other}", null, 404),
                ("GET", $"/services/{own}/inspection/photos/{other}/url", null, 404),
                ("DELETE", $"/services/{own}/inspection/photos/{other}", null, 404),
            };
            foreach (var row in rows) yield return new object[] { tenant, role, row.Method, row.Path, row.Body!, row.Status };
        }
    }

    [Theory]
    [MemberData(nameof(CrossTenantCases))]
    public async Task ForeignResource_IsRejectedWithoutBusinessWrites(int tenant, string role, string method, string path, string? body, int status)
    {
        var state = new TenantIsolationStore();
        // A direct service without an invoice reaches the budget ownership check.
        if (path == "/invoices" && body?.Contains("budgetId") == true)
            state.Invoices.RemoveAll(x => x.ServiceId == tenant * 100 + 1);
        var before = state.Snapshot();
        using var host = Host(state);
        using var client = Client(host, role, tenant, tenant * 1000 + 1);
        using var response = await Send(client, method, path, body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"{role}@{tenant}: {method} {path}: {response.StatusCode} {text}");
        using var json = JsonDocument.Parse(text);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(status, json.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.DoesNotContain("PRIVATE-", text);
        Assert.Empty(state.Writes);
        Assert.Equal(before, state.Snapshot());
        Assert.Equal(0, state.StorageCalls);
        Assert.Equal(0, state.DetailReads);
        Assert.NotEmpty(state.ScopedReads);
        Assert.All(state.ScopedReads, read => Assert.Equal(tenant, read.Tenant));
        Assert.Equal(0, state.InspectionReads); // Foreign parent rejected before inspection handler.
        if (path.Contains("/inspection/photos/") && path.StartsWith($"/services/{tenant * 100 + 1}/"))
            Assert.Equal((tenant * 100 + 1, (3 - tenant) * 100 + 1), Assert.Single(state.PhotoReads));
        else Assert.Empty(state.PhotoReads);
    }

    public static IEnumerable<object[]> OwnReadCases()
    {
        foreach (var tenant in new[] { 1, 2 })
        foreach (var resource in new[] { "clients", "vehicles", "budgets", "services", "invoices", "inspection", "photo" })
            yield return new object[] { tenant, resource };
    }

    [Theory]
    [MemberData(nameof(OwnReadCases))]
    public async Task OwnResource_RemainsAccessible(int tenant, string resource)
    {
        var state = new TenantIsolationStore();
        using var host = Host(state);
        using var client = Client(host, "dueno", tenant, tenant * 1000 + 1);
        var id = tenant * 100 + 1;
        var path = resource == "inspection" ? $"/services/{id}/inspection" : resource == "photo" ? $"/services/{id}/inspection/photos/{id}/url" : $"/{resource}/{id}";
        using var response = await client.GetAsync(path);
        Assert.Equal(200, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Empty(state.Writes);
        Assert.NotEmpty(state.ScopedReads);
        Assert.All(state.ScopedReads, read => Assert.Equal(tenant, read.Tenant));
        if (resource == "photo") Assert.Equal(1, state.StorageCalls);
    }

    public static IEnumerable<object[]> ListCases()
    {
        foreach (var tenant in new[] { 1, 2 })
        foreach (var resource in new[] { "clients", "vehicles", "budgets", "services", "invoices" })
            yield return new object[] { tenant, resource };
    }

    [Theory]
    [MemberData(nameof(ListCases))]
    public async Task Lists_IgnoreAttemptToSelectAnotherTenant(int tenant, string resource)
    {
        var state = new TenantIsolationStore();
        using var host = Host(state);
        using var client = Client(host, "empleado", tenant, tenant * 1000 + 1);
        using var response = await client.GetAsync($"/{resource}?tenantId={3 - tenant}");
        Assert.Equal(200, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        var items = resource == "services" ? data : data.GetProperty("items");
        Assert.Equal(tenant == 1 ? 2 : 1, items.GetArrayLength());
        foreach (var row in items.EnumerateArray()) Assert.Equal(tenant.ToString(), row.GetProperty("tenantId").GetString());
        Assert.All(state.ScopedReads, read => Assert.Equal(tenant, read.Tenant));
        Assert.Empty(state.Writes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CreateClient_IgnoresTenantInBody_AndLeavesOtherTenantUnchanged(int tenant)
    {
        var state = new TenantIsolationStore();
        var foreignBefore = JsonSerializer.Serialize(state.Clients.Where(c => c.TenantId != tenant));
        using var host = Host(state);
        using var client = Client(host, "dueno", tenant, tenant * 1000 + 1);
        using var response = await Send(client, "POST", "/clients", $"{{\"name\":\"New\",\"tenantId\":{3 - tenant}}}");
        Assert.Equal(201, (int)response.StatusCode);
        Assert.Equal(tenant, state.Clients.Single(c => c.ClientId == 999).TenantId);
        Assert.Equal(foreignBefore, JsonSerializer.Serialize(state.Clients.Where(c => c.TenantId != tenant)));
        Assert.Equal("client-create", Assert.Single(state.Writes));
    }

    [Theory]
    [InlineData(101)]
    [InlineData(102)]
    [InlineData(201)]
    public async Task Portal_UsesSubject_NotClientOrTenantQuery(int subject)
    {
        var state = new TenantIsolationStore();
        // A draft belonging to this client's vehicle must still remain private.
        state.Budgets.Add(new BoxService_BackEnd.Models.Budget { BudgetId = 900, TenantId = subject / 100, VehicleId = subject, Status = "draft" });
        var before = state.Snapshot();
        using var host = Host(state);
        using var client = Client(host, "customer", subject / 100, subject);
        var otherClient = subject == 101 ? 102 : 101;
        using var response = await client.GetAsync($"/portal/me?clientId={otherClient}&tenantId={3 - subject / 100}&sub={otherClient}&vehicleId={otherClient}");
        Assert.Equal(200, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(subject, data.GetProperty("client").GetProperty("clientId").GetInt32());
        var row = Assert.Single(data.GetProperty("vehicles").EnumerateArray());
        Assert.Equal(subject, row.GetProperty("vehicle").GetProperty("vehicleId").GetInt32());
        Assert.Equal(subject, Assert.Single(row.GetProperty("budgets").EnumerateArray()).GetProperty("budget").GetProperty("budgetId").GetInt32());
        Assert.Equal(subject, Assert.Single(row.GetProperty("services").EnumerateArray()).GetProperty("serviceId").GetInt32());
        Assert.Equal(subject, Assert.Single(state.PortalClientReads));
        Assert.All(state.ScopedReads, read => { Assert.Equal(subject / 100, read.Tenant); Assert.Equal(subject, read.Id); });
        Assert.Equal(before, state.Snapshot());
        Assert.Empty(state.Writes);
    }

    [Theory]
    [InlineData("GET", "/clients/201", null)]
    [InlineData("GET", "/vehicles/201", null)]
    [InlineData("GET", "/budgets/201", null)]
    [InlineData("GET", "/services/201/inspection/photos/201/url", null)]
    [InlineData("PATCH", "/invoices/201", "{\"status\":\"paid\"}")]
    [InlineData("POST", "/vehicles/201/transfer", "{\"newClientId\":101}")]
    public async Task PortalCustomer_CannotUseStaffEndpoints(string method, string path, string? body)
    {
        var state = new TenantIsolationStore();
        var before = state.Snapshot();
        using var host = Host(state);
        using var client = Client(host, "customer", 1, 101);
        using var response = await Send(client, method, path, body);
        Assert.Equal(403, (int)response.StatusCode);
        Assert.Equal(before, state.Snapshot());
        Assert.Empty(state.Writes);
        Assert.Empty(state.ScopedReads);
        Assert.Equal(0, state.StorageCalls);
    }

    [Fact]
    public async Task TransferWithinTenant_PreservesHistory_AndPortalFollowsNewOwner()
    {
        var state = new TenantIsolationStore();
        var history = JsonSerializer.Serialize(new { state.Budgets, state.Services, state.Invoices });
        using var host = Host(state);
        using var owner = Client(host, "dueno", 1, 1001);
        using var response = await Send(owner, "POST", "/vehicles/101/transfer", "{\"newClientId\":102}");
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("vehicle-owner", Assert.Single(state.Writes));
        Assert.Equal(102, state.Vehicles.Single(v => v.VehicleId == 101).ClientId);
        Assert.Equal(history, JsonSerializer.Serialize(new { state.Budgets, state.Services, state.Invoices }));
        using var oldOwner = Client(host, "customer", 1, 101);
        using var oldResponse = await oldOwner.GetAsync("/portal/me");
        Assert.Equal(200, (int)oldResponse.StatusCode);
        using var oldJson = JsonDocument.Parse(await oldResponse.Content.ReadAsStringAsync());
        Assert.Empty(oldJson.RootElement.GetProperty("data").GetProperty("vehicles").EnumerateArray());
        using var newOwner = Client(host, "customer", 1, 102);
        using var newResponse = await newOwner.GetAsync("/portal/me");
        Assert.Equal(200, (int)newResponse.StatusCode);
        using var newJson = JsonDocument.Parse(await newResponse.Content.ReadAsStringAsync());
        var transferred = newJson.RootElement.GetProperty("data").GetProperty("vehicles").EnumerateArray().Single(v => v.GetProperty("vehicle").GetProperty("vehicleId").GetInt32() == 101);
        Assert.Equal(101, Assert.Single(transferred.GetProperty("services").EnumerateArray()).GetProperty("serviceId").GetInt32());
    }

    private WebApplicationFactory<AuthService> Host(TenantIsolationStore state) => factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
    {
        services.AddSingleton<ClientRepository>(new TenantIsolationStore.ClientsRepo(state));
        services.AddSingleton<VehicleRepository>(new TenantIsolationStore.VehiclesRepo(state));
        services.AddSingleton<BudgetRepository>(new TenantIsolationStore.BudgetsRepo(state));
        services.AddSingleton<ServicesRepository>(new TenantIsolationStore.ServicesRepo(state));
        services.AddSingleton<InvoiceRepository>(new TenantIsolationStore.InvoicesRepo(state));
        services.AddSingleton<ServiceInspectionRepository>(new TenantIsolationStore.InspectionsRepo(state));
        services.AddSingleton<SucursalRepository>(new TenantIsolationStore.BranchesRepo());
        services.AddSingleton<SupabaseStorageService>(new TenantIsolationStore.Storage(state));
        services.AddSingleton<AuthAuditRepository>(new TenantIsolationStore.AuditRepo(state));
    }));

    private HttpClient Client(WebApplicationFactory<AuthService> host, string role, int tenant, int subject)
    {
        using var tokenClient = factory.Client(role, tenant, subject);
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = tokenClient.DefaultRequestHeaders.Authorization;
        return client;
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, string method, string path, string? body)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body == "multipart")
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(new byte[] { 1, 2, 3 });
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(file, "file", "photo.jpg");
            form.Add(new StringContent("front"), "photoType");
            request.Content = form;
        }
        else if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }
}
