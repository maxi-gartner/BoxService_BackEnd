using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Services;
using Xunit;

namespace BoxService_BackEnd.UnitTests.Services;

public class ClientServiceTests
{
    // Construction does not open a connection. Every case below exits during
    // validation, before the repository is called. Do not add successful
    // creation cases here without first isolating persistence in a later stage.
    private readonly ClientService _service = new(new ClientRepository());

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "")]
    [InlineData(null, "   ")]
    [InlineData("", "Valid fallback")]
    [InlineData("   ", "Valid fallback")]
    public void Create_WithInvalidEffectiveName_ThrowsArgumentException(
        string? nombre, string? name)
    {
        // Nombre takes precedence when non-null, even when empty.
        var request = new ClientCreateRequest
        {
            Nombre = nombre,
            Name = name,
            Email = "cliente@example.com"
        };

        var exception = Assert.Throws<ArgumentException>(() => _service.Create(1, request));

        Assert.Equal("Name is required.", exception.Message);
    }

    [Theory]
    [InlineData("cliente.example.com")]
    [InlineData("@example.com")]
    [InlineData("cliente@")]
    [InlineData("  @example.com  ")]
    [InlineData("  cliente@  ")]
    public void Create_WithInvalidEmail_ThrowsArgumentException(string email)
    {
        var request = new ClientCreateRequest { Name = "Cliente", Email = email };

        var exception = Assert.Throws<ArgumentException>(() => _service.Create(1, request));

        Assert.Equal("Email is invalid.", exception.Message);
    }
}
