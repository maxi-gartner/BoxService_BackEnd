using System;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BoxService_BackEnd.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace BoxService_BackEnd.UnitTests.Api;

// Uses the production entry point (via a public type in its assembly).
// No production authentication, endpoint or middleware source is changed.
public sealed class SecurityRegressionFactory : WebApplicationFactory<AuthService>
{
    private const string Key = "security-regression-only-key-not-for-production-2026";
    private readonly Socket _reservedPort = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    public ConcurrentQueue<Exception> Exceptions { get; } = new();

    public SecurityRegressionFactory()
    {
        // Bound, exclusively reserved, deliberately NOT listening: no database can
        // occupy this port. Audit writes fail with NpgsqlException, already handled
        // by the production audit middleware, without masking the HTTP result.
        _reservedPort.ExclusiveAddressUse = true;
        _reservedPort.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var port = ((IPEndPoint)_reservedPort.LocalEndPoint!).Port;
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            $"Host=127.0.0.1;Port={port};Database=security_tests;Username=test;Password=test;Timeout=1;Pooling=false");
        builder.UseSetting("Jwt:Key", Key);
        builder.UseSetting("Jwt:Issuer", "security-tests");
        builder.UseSetting("Jwt:Audience", "security-tests");
        builder.ConfigureTestServices(services =>
        {
            // Real JWT signature/issuer/audience/lifetime checks remain enabled.
            // SessionValidation's database lookup is outside these regressions.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                options => options.Events.OnTokenValidated = _ => Task.CompletedTask);
            services.AddSingleton<ILoggerProvider>(new ExceptionLoggerProvider(Exceptions));
        });
    }

    public HttpClient Client(string? role = null, int tenantId = 7, int subject = 1)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (role is not null)
        {
            var claims = new System.Collections.Generic.List<Claim>
            {
                new("sub", subject.ToString()), new("name", "regression-test"), new("role", role)
            };
            if (role is not ("superadmin" or "customer")) claims.Add(new Claim("tenantId", tenantId.ToString()));
            var token = new JwtSecurityToken("security-tests", "security-tests", claims,
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _reservedPort.Dispose();
    }

    private sealed class ExceptionLoggerProvider(ConcurrentQueue<Exception> exceptions) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ExceptionLogger(exceptions);
        public void Dispose() { }
    }

    private sealed class ExceptionLogger(ConcurrentQueue<Exception> exceptions) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) exceptions.Enqueue(exception);
        }
    }
}
