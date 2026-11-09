using System;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Services;
using BoxService_BackEnd.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BoxService_BackEnd.UnitTests.Services;

public class ClientDependencyInjectionTests
{
    [Fact]
    public void ScopedRegistrations_ResolveServiceAndShareRepositoryOnlyWithinScope()
    {
        // Reproduce the three registrations from Program.cs without starting the web host.
        // ClientRepository construction does not open a connection; no valid request is sent.
        var services = new ServiceCollection();
        services.AddScoped<ClientRepository>();
        services.AddScoped<IClientRepository>(sp => sp.GetRequiredService<ClientRepository>());
        services.AddScoped<ClientService>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var concrete = firstScope.ServiceProvider.GetRequiredService<ClientRepository>();
        var abstraction = firstScope.ServiceProvider.GetRequiredService<IClientRepository>();
        var service = firstScope.ServiceProvider.GetRequiredService<ClientService>();

        Assert.Same(concrete, abstraction);
        Assert.Same(service, firstScope.ServiceProvider.GetRequiredService<ClientService>());
        Assert.NotSame(abstraction, secondScope.ServiceProvider.GetRequiredService<IClientRepository>());
        Assert.NotSame(service, secondScope.ServiceProvider.GetRequiredService<ClientService>());
        var error = Assert.Throws<ArgumentException>(() => service.Create(1, new ClientCreateRequest()));
        Assert.Equal("Name is required.", error.Message);
    }
}
