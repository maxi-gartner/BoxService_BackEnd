using BoxService_BackEnd.Models;
using System;
using System.Collections.Generic;
using BoxService_BackEnd.Services;
using BoxService_BackEnd.Services.Abstractions;
using Xunit;

namespace BoxService_BackEnd.UnitTests.Services;

public class ClientServiceTests
{
    private readonly ManualClientRepository _repository = new();
    private readonly ClientService _service;

    public ClientServiceTests() => _service = new ClientService(_repository);

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
        Assert.Empty(_repository.Calls);
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
        Assert.Empty(_repository.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" cliente@example.com ")]
    public void Create_WithValidRequest_ForwardsUnchangedRequestAndReturnsRepositoryResult(string? email)
    {
        var request = new ClientCreateRequest
        {
            Nombre = " Nombre ", Name = "Fallback", Telefono = " 123 ", Phone = "456", Email = email
        };
        var expected = new Client { ClientId = 42, Name = "Persisted" };
        _repository.OnCreate = (tenantId, actual) =>
        {
            Assert.Equal(7, tenantId);
            Assert.Same(request, actual);
            Assert.Equal(" Nombre ", actual.Nombre);
            Assert.Equal("Fallback", actual.Name);
            Assert.Equal(" 123 ", actual.Telefono);
            Assert.Equal("456", actual.Phone);
            Assert.Equal(email, actual.Email);
            return expected;
        };

        Assert.Same(expected, _service.Create(7, request));
        Assert.Equal(new[] { "Create" }, _repository.Calls);
    }

    [Fact]
    public void Create_WithRepositoryError_PropagatesSameException()
    {
        var expected = new InvalidOperationException("Persistence failure");
        _repository.OnCreate = (_, _) => throw expected;

        var actual = Assert.Throws<InvalidOperationException>(() =>
            _service.Create(7, new ClientCreateRequest { Name = "Cliente" }));

        Assert.Same(expected, actual);
        Assert.Equal(new[] { "Create" }, _repository.Calls);
    }

    [Theory]
    [InlineData(-2, -5, 1, 1)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(1, 1, 1, 1)]
    [InlineData(3, 25, 3, 25)]
    [InlineData(2, 200, 2, 200)]
    [InlineData(2, 201, 2, 200)]
    public void ListPaged_PreservesBoundsAndForwardsSearch(int page, int size, int expectedPage, int expectedSize)
    {
        var items = new List<Client> { new() { ClientId = 42 } };
        _repository.OnGetPaged = (tenantId, actualPage, actualSize, search) =>
        {
            Assert.Equal(7, tenantId);
            Assert.Equal(expectedPage, actualPage);
            Assert.Equal(expectedSize, actualSize);
            Assert.Equal(" search ", search);
            return (items, 53);
        };

        var result = _service.ListPaged(7, page, size, " search ");

        Assert.Same(items, result.Items);
        Assert.Equal(53, result.TotalCount);
        Assert.Equal(new[] { "GetPaged" }, _repository.Calls);
    }

    [Fact]
    public void GetVehicles_WithMissingClient_DoesNotQueryVehicles()
    {
        _repository.OnGetById = (id, tenantId) =>
        {
            Assert.Equal(42, id);
            Assert.Equal(7, tenantId);
            return null;
        };

        Assert.Null(_service.GetVehicles(42, 7));
        Assert.Equal(new[] { "GetById" }, _repository.Calls);
    }

    [Fact]
    public void GetVehicles_WithExistingClient_ReturnsVehiclesAfterCheckingClient()
    {
        var expected = new List<ClientVehicle> { new() { VehicleId = 9 } };
        _repository.OnGetById = (id, tenantId) =>
        {
            Assert.Equal(42, id);
            Assert.Equal(7, tenantId);
            return new Client { ClientId = id };
        };
        _repository.OnGetVehicles = (id, tenantId) =>
        {
            Assert.Equal(42, id);
            Assert.Equal(7, tenantId);
            return expected;
        };

        Assert.Same(expected, _service.GetVehicles(42, 7));
        Assert.Equal(new[] { "GetById", "GetVehiclesByClientId" }, _repository.Calls);
    }

    [Fact]
    public void GetVehicles_WhenClientLookupFails_PropagatesErrorWithoutQueryingVehicles()
    {
        var expected = new InvalidOperationException("Lookup failure");
        _repository.OnGetById = (_, _) => throw expected;

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => _service.GetVehicles(42, 7)));
        Assert.Equal(new[] { "GetById" }, _repository.Calls);
    }

    [Fact]
    public void List_ForwardsTenantAndReturnsRepositoryList()
    {
        var expected = new List<Client> { new() { ClientId = 42 } };
        _repository.OnGetAll = tenantId =>
        {
            Assert.Equal(7, tenantId);
            return expected;
        };

        Assert.Same(expected, _service.List(7));
        Assert.Equal(new[] { "GetAll" }, _repository.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetById_ForwardsIdentifiersAndReturnsNullableResult(bool exists)
    {
        var expected = exists ? new Client { ClientId = 42 } : null;
        _repository.OnGetById = (id, tenantId) =>
        {
            Assert.Equal(42, id);
            Assert.Equal(7, tenantId);
            return expected;
        };

        Assert.Same(expected, _service.GetById(42, 7));
        Assert.Equal(new[] { "GetById" }, _repository.Calls);
    }

    [Fact]
    public void ListAllAdmin_ReturnsRepositoryResults()
    {
        var expected = new List<(Client Client, string TenantName)> { (new Client(), "Taller") };
        _repository.OnGetAllAdmin = () => expected;

        Assert.Same(expected, _service.ListAllAdmin());
        Assert.Equal(new[] { "GetAllAdmin" }, _repository.Calls);
    }

    // Unconfigured operations fail immediately: no real persistence is available.
    private sealed class ManualClientRepository : IClientRepository
    {
        public List<string> Calls { get; } = new();
        public Func<int, ClientCreateRequest, Client> OnCreate { get; set; } = (_, _) => throw new InvalidOperationException("Unexpected Create");
        public Func<int, int, int, string?, (List<Client> Items, int TotalCount)> OnGetPaged { get; set; } = (_, _, _, _) => throw new InvalidOperationException("Unexpected GetPaged");
        public Func<int, int, Client?> OnGetById { get; set; } = (_, _) => throw new InvalidOperationException("Unexpected GetById");
        public Func<int, int, List<ClientVehicle>> OnGetVehicles { get; set; } = (_, _) => throw new InvalidOperationException("Unexpected GetVehicles");
        public Func<int, List<Client>> OnGetAll { get; set; } = _ => throw new InvalidOperationException("Unexpected GetAll");
        public Func<List<(Client Client, string TenantName)>> OnGetAllAdmin { get; set; } = () => throw new InvalidOperationException("Unexpected GetAllAdmin");

        public Client Create(int tenantId, ClientCreateRequest request)
        {
            Calls.Add(nameof(Create));
            return OnCreate(tenantId, request);
        }

        public (List<Client> Items, int TotalCount) GetPaged(int tenantId, int page, int pageSize, string? search)
        {
            Calls.Add(nameof(GetPaged));
            return OnGetPaged(tenantId, page, pageSize, search);
        }

        public Client? GetById(int id, int tenantId)
        {
            Calls.Add(nameof(GetById));
            return OnGetById(id, tenantId);
        }

        public List<ClientVehicle> GetVehiclesByClientId(int clientId, int tenantId)
        {
            Calls.Add(nameof(GetVehiclesByClientId));
            return OnGetVehicles(clientId, tenantId);
        }

        public List<Client> GetAll(int tenantId)
        {
            Calls.Add(nameof(GetAll));
            return OnGetAll(tenantId);
        }

        public List<(Client Client, string TenantName)> GetAllAdmin()
        {
            Calls.Add(nameof(GetAllAdmin));
            return OnGetAllAdmin();
        }
    }
}
