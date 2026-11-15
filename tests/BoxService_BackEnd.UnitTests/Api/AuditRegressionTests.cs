using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using BoxService_BackEnd.Api;
using BoxService_BackEnd.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BoxService_BackEnd.UnitTests.Api;

[Collection("Security regression")]
public sealed class AuditRegressionTests(SecurityRegressionFactory factory)
{
    [Theory]
    [InlineData("GET", "/clients/123", null, 401, "/clients/{id:int}")]
    [InlineData("GET", "/portal/me", null, 401, "/portal/me")]
    [InlineData("GET", "/clients/123", "customer", 403, "/clients/{id:int}")]
    [InlineData("POST", "/users", null, 401, "/users")]
    [InlineData("POST", "/USERS/", "empleado", 403, "/users")]
    [InlineData("POST", "/users", "superadmin", 403, "/users")]
    [InlineData("PATCH", "/users/123", "empleado", 403, "/users/{id:int}")]
    [InlineData("POST", "/auth/logout", null, 401, "/auth/logout")]
    public async Task ProtectedEndpoints_RecordExactlyOneDenial(string method, string path, string? role, int status, string template)
    {
        var audit = new RecordingAudit();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<AuthAuditRepository>(audit)));
        using var client = host.CreateClient();
        using var identity = factory.Client(role);
        client.DefaultRequestHeaders.Authorization = identity.DefaultRequestHeaders.Authorization;
        using var request = new HttpRequestMessage(new HttpMethod(method), path + "?token=SECRET_QUERY");
        request.Headers.Add("X-Secret", "SECRET_HEADER");
        using var response = await client.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
        var item = Assert.Single(audit.Events);
        Assert.Equal(("access_denied", status.ToString(), method, template), item);
        Assert.DoesNotContain("SECRET", string.Join("|", item));
        if (method == "POST" && role is null) Assert.True(response.Headers.Contains("WWW-Authenticate"));
    }

    [Fact]
    public async Task Logout_KeepsItsOwnSingleEvent()
    {
        var audit = new RecordingAudit();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<AuthAuditRepository>(audit)));
        using var client = host.CreateClient();
        using var identity = factory.Client("dueno");
        client.DefaultRequestHeaders.Authorization = identity.DefaultRequestHeaders.Authorization;
        using var response = await client.PostAsync("/auth/logout", null);
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("logout", Assert.Single(audit.Events).Type);
    }

    [Theory]
    [InlineData("/auth/login", "{\"username\":\"\",\"password\":\"SECRET_PASSWORD\"}", 401, "login")]
    [InlineData("/auth/forgot-password", "{\"email\":\"\"}", 202, "password_reset_requested")]
    [InlineData("/auth/reset-password", "{\"token\":\"\",\"newPassword\":\"SECRET_PASSWORD\"}", 400, "password_reset_completed")]
    public async Task RealAuthHandlers_KeepTheirOwnSingleEventWithoutExternalAccess(string route, string body, int status, string eventType)
    {
        var audit = new RecordingAudit();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<AuthAuditRepository>(audit)));
        using var client = host.CreateClient();
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(route, content);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(eventType, Assert.Single(audit.Events).Type);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task PersistenceFailure_PreservesStatusBodyAndHeaders_AndSanitizesLog(int status)
    {
        var audit = new RecordingAudit { Fail = true };
        var logger = new CaptureLogger();
        var context = Context("/clients/{id:int}", "GET");
        var middleware = new ApiMutationAuditMiddleware(async c =>
        {
            c.Response.StatusCode = status;
            c.Response.Headers.WWWAuthenticate = "Bearer";
            await c.Response.WriteAsync("original-json-response");
        }, logger);
        await middleware.InvokeAsync(context, audit);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("Bearer", context.Response.Headers.WWWAuthenticate);
        context.Response.Body.Position = 0;
        Assert.Equal("original-json-response", await new StreamReader(context.Response.Body).ReadToEndAsync());
        Assert.Null(logger.Exception);
        Assert.Contains("Audit persistence failed", logger.Message);
        Assert.DoesNotContain("SECRET", logger.Message);
        Assert.DoesNotContain("123", logger.Message);
    }

    [Theory]
    [InlineData("/auth/login", "login")]
    [InlineData("/auth/forgot-password", "password_reset_requested")]
    [InlineData("/auth/reset-password", "password_reset_completed")]
    [InlineData("/users/{id:int}/reset-password", "admin_password_reset")]
    public async Task AuthOwnedEvents_AreNotDuplicated(string route, string eventType)
    {
        var audit = new RecordingAudit();
        var context = Context(route, "POST");
        var middleware = new ApiMutationAuditMiddleware(c =>
        {
            audit.Record(eventType, "succeeded", null, null, null, null);
            c.Response.StatusCode = route == "/auth/login" ? 401 : 200;
            return Task.CompletedTask;
        }, new CaptureLogger());
        await middleware.InvokeAsync(context, audit);
        Assert.Equal(eventType, Assert.Single(audit.Events).Type);
    }

    [Theory]
    [InlineData(201, "succeeded")]
    [InlineData(400, "failed")]
    [InlineData(403, "403")]
    public async Task Mutation_RecordsOnlyOneSafeEvent(int status, string outcome)
    {
        var audit = new RecordingAudit();
        var context = Context("/clients/{id:int}", "POST");
        var middleware = new ApiMutationAuditMiddleware(c => { c.Response.StatusCode = status; return Task.CompletedTask; }, new CaptureLogger());
        await middleware.InvokeAsync(context, audit);
        var item = Assert.Single(audit.Events);
        Assert.Equal(outcome, item.Outcome);
        Assert.Equal(status == 403 ? "access_denied" : "business_change", item.Type);
        Assert.Equal("/clients/{id:int}", item.Route);
    }

    [Theory]
    [InlineData("/portal/auth/google", 401)]
    [InlineData("/health", 403)]
    [InlineData(null, 401)]
    public async Task PublicOrUnknownRoutes_DoNotGenerateDenials(string? route, int status)
    {
        var audit = new RecordingAudit();
        var context = Context(route, "GET");
        await new ApiMutationAuditMiddleware(c => { c.Response.StatusCode = status; return Task.CompletedTask; }, new CaptureLogger()).InvokeAsync(context, audit);
        Assert.Empty(audit.Events);
    }

    private static DefaultHttpContext Context(string? route, string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/clients/123";
        context.Request.QueryString = new QueryString("?token=SECRET_QUERY");
        context.Request.Headers.Authorization = "Bearer SECRET_TOKEN";
        context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("password=SECRET_PASSWORD"));
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "safe-trace";
        if (route is not null) context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(route), 0, EndpointMetadataCollection.Empty, "test"));
        return context;
    }

    private sealed class RecordingAudit : AuthAuditRepository
    {
        public bool Fail { get; init; }
        public List<(string Type, string Outcome, string? Method, string? Route)> Events { get; } = new();
        public override void Record(string eventType, string outcome, int? actorUserId, int? targetUserId, int? tenantId, string? sourceIp, string? httpMethod = null, string? resourcePath = null)
        {
            if (Fail) throw new InvalidOperationException("SECRET_PASSWORD https://storage.test/signed?token=SECRET_TOKEN");
            Events.Add((eventType, outcome, httpMethod, resourcePath));
        }
    }

    private sealed class CaptureLogger : ILogger<ApiMutationAuditMiddleware>
    {
        public string Message { get; private set; } = "";
        public Exception? Exception { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { Message = formatter(state, exception); Exception = exception; }
    }
}
