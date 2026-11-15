using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BoxService_BackEnd.UnitTests.Api;

// DatabaseConnection is static; keep this host isolated from other collections.
[CollectionDefinition("Security regression", DisableParallelization = true)]
public sealed class SecurityRegressionCollection : ICollectionFixture<SecurityRegressionFactory> { }

[Collection("Security regression")]
public sealed class SecurityRegressionTests(SecurityRegressionFactory factory, ITestOutputHelper output)
{
    [Theory]
    [InlineData("/users")]
    [InlineData("/USERS")]
    [InlineData("/Users")]
    [InlineData("/users/")]
    [InlineData("/USERS/")]
    [InlineData("/Users/")]
    public async Task Employee_CreateUser_AllRouteCasingsMustBeForbidden(string path)
    {
        using var client = factory.Client("empleado");
        // A blank username is intentional: if authorization is bypassed, the real
        // handler returns its validation error BEFORE any user database operation.
        using var response = await client.PostAsJsonAsync(path, new { username = "", password = "test-only", name = "Test" });
        var body = await response.Content.ReadAsStringAsync();
        output.WriteLine($"POST {path}: {(int)response.StatusCode} {body}");
        if (response.StatusCode != HttpStatusCode.Forbidden)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var json = JsonDocument.Parse(body);
            Assert.Equal("El usuario es obligatorio.", json.RootElement.GetProperty("error").GetProperty("message").GetString());
        }
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Positive authorization control: the owner still reaches validation for
        // every casing, without querying or inserting users in a database.
        using var owner = factory.Client("dueno");
        using var ownerResponse = await owner.PostAsJsonAsync(path, new { username = "", password = "test-only", name = "Test" });
        Assert.Equal(HttpStatusCode.BadRequest, ownerResponse.StatusCode);
        using var ownerJson = JsonDocument.Parse(await ownerResponse.Content.ReadAsStringAsync());
        Assert.Equal("El usuario es obligatorio.", ownerJson.RootElement.GetProperty("error").GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("/users", false, 401)] // Manual middleware response.
    [InlineData("/clients", false, 401)] // RequireAuthorization challenge.
    [InlineData("/tenants", true, 403)] // Manual role restriction.
    [InlineData("/metrics", true, 403)] // RequireRole forbid.
    public async Task AuthorizationErrors_MustUseApiEnvelope(string path, bool employee, int status)
    {
        using var client = factory.Client(employee ? "empleado" : null);
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        output.WriteLine($"GET {path}: {(int)response.StatusCode}; Content-Type={response.Content.Headers.ContentType}; body={body}");
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(body);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").ValueKind);
        Assert.Equal(status, json.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("error").GetProperty("message").GetString()));

        if (path == "/clients")
        {
            Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
            client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid-token");
            using var invalid = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
            Assert.Contains(invalid.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
            Assert.Equal("application/json", invalid.Content.Headers.ContentType?.MediaType);
            using var invalidJson = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
            Assert.False(invalidJson.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(401, invalidJson.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        }
    }

    [Fact]
    public async Task Superadmin_CreateUserWithoutTenant_MustBeRejectedWithoutServerError()
    {
        using var client = factory.Client("superadmin");
        using var response = await client.PostAsJsonAsync("/users", new { username = "", password = "test-only", name = "Test" });
        var body = await response.Content.ReadAsStringAsync();
        output.WriteLine($"POST /users (superadmin): {(int)response.StatusCode} {body}");
        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            // Distinguish the actual defect from startup/DB infrastructure failures.
            Assert.Contains(factory.Exceptions, e => e is InvalidOperationException
                && e.Message == "El token no tiene tenantId."
                && e.StackTrace?.Contains("HttpContextExtensions.TenantId") == true);
        }
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Superadmin retains its designated route. Invalid username stops before
        // database access, demonstrating that authorization allowed the handler.
        using var tenantResponse = await client.PostAsJsonAsync("/tenants/7/users",
            new { username = "", password = "test-only", name = "Test", role = "empleado" });
        Assert.Equal(HttpStatusCode.BadRequest, tenantResponse.StatusCode);
        using var tenantJson = JsonDocument.Parse(await tenantResponse.Content.ReadAsStringAsync());
        Assert.Equal("El usuario es obligatorio.", tenantJson.RootElement.GetProperty("error").GetProperty("message").GetString());
    }
}
