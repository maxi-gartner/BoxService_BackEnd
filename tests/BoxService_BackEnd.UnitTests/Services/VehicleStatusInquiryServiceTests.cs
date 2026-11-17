using System;
using System.Collections.Generic;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Services;
using Xunit;

namespace BoxService_BackEnd.UnitTests.Services;

public class VehicleStatusInquiryServiceTests
{
    private readonly FakeVehicleStatusInquiryRepository _repository = new();
    private readonly FakeVehicleRepository _vehicles = new();
    private readonly FakeWorkOrderRepository _workOrders = new();
    private readonly VehicleStatusInquiryService _service;

    public VehicleStatusInquiryServiceTests() => _service = new VehicleStatusInquiryService(_repository, _vehicles, _workOrders);

    private void VehicleExists() => _vehicles.OnGetById = (_, _) => new Vehicle { VehicleId = 41, ClientId = 31, TenantId = 1 };

    [Fact]
    public void Create_WhenVehicleDoesNotExist_ReturnsNotFound_AndNeverChecksTheWorkOrderOrInquiryRepository()
    {
        _vehicles.OnGetById = (_, _) => null;

        var (ok, rateLimited, notFound, notReceived, result) = _service.Create(tenantId: 1, vehicleId: 41, clientId: 31);

        Assert.False(ok);
        Assert.False(rateLimited);
        Assert.True(notFound);
        Assert.False(notReceived);
        Assert.Null(result);
        Assert.False(_workOrders.Called);
        Assert.Empty(_repository.Calls);
    }

    [Fact]
    public void Create_WhenVehicleBelongsToAnotherClient_ReturnsNotFound_AndNeverHitsTheInquiryRepository()
    {
        _vehicles.OnGetById = (_, _) => new Vehicle { VehicleId = 41, ClientId = 999, TenantId = 1 };

        var (ok, rateLimited, notFound, notReceived, result) = _service.Create(tenantId: 1, vehicleId: 41, clientId: 31);

        Assert.False(ok);
        Assert.False(rateLimited);
        Assert.True(notFound);
        Assert.False(notReceived);
        Assert.Null(result);
        Assert.Empty(_repository.Calls);
    }

    [Fact]
    public void Create_WhenNeverReceived_ReturnsNotReceived_AndNeverHitsTheInquiryRepository()
    {
        VehicleExists();
        _workOrders.OnGetLatestForVehicle = (_, _) => null;

        var (ok, rateLimited, notFound, notReceived, result) = _service.Create(tenantId: 1, vehicleId: 41, clientId: 31);

        Assert.False(ok);
        Assert.False(rateLimited);
        Assert.False(notFound);
        Assert.True(notReceived);
        Assert.Null(result);
        Assert.Empty(_repository.Calls);
    }

    [Fact]
    public void Create_WhenAlreadyDelivered_ReturnsNotReceived_AndNeverHitsTheInquiryRepository()
    {
        VehicleExists();
        _workOrders.OnGetLatestForVehicle = (_, _) => new WorkOrder { OrderId = 9, Status = "entregado" };

        var (ok, rateLimited, notFound, notReceived, result) = _service.Create(tenantId: 1, vehicleId: 41, clientId: 31);

        Assert.False(ok);
        Assert.False(rateLimited);
        Assert.False(notFound);
        Assert.True(notReceived);
        Assert.Null(result);
        Assert.Empty(_repository.Calls);
    }

    [Theory]
    [InlineData("en_espera")]
    [InlineData("en_proceso")]
    [InlineData("terminado")]
    public void Create_WhenCurrentlyReceived_ChecksTheInquiryRepository(string status)
    {
        VehicleExists();
        _workOrders.OnGetLatestForVehicle = (_, _) => new WorkOrder { OrderId = 9, Status = status };
        _repository.OnCreate = (_, _, _) => null; // solo nos importa que haya llegado hasta acá

        var (_, rateLimited, notFound, notReceived, _) = _service.Create(tenantId: 1, vehicleId: 41, clientId: 31);

        Assert.True(rateLimited);
        Assert.False(notFound);
        Assert.False(notReceived);
        Assert.Single(_repository.Calls);
    }

    [Fact]
    public void Create_WhenRepositoryDeclinesTheInsert_ReturnsRateLimited()
    {
        // El repo devuelve null cuando el INSERT...WHERE NOT EXISTS no
        // inserta nada — ya hubo una consulta de este vehículo hace
        // menos de una hora.
        VehicleExists();
        _workOrders.OnGetLatestForVehicle = (_, _) => new WorkOrder { OrderId = 9, Status = "en_espera" };
        _repository.OnCreate = (_, _, _) => null;

        var (ok, rateLimited, notFound, notReceived, result) = _service.Create(tenantId: 1, vehicleId: 41, clientId: 31);

        Assert.False(ok);
        Assert.True(rateLimited);
        Assert.False(notFound);
        Assert.False(notReceived);
        Assert.Null(result);
    }

    [Fact]
    public void Create_WhenValid_ReturnsOkWithTheCreatedInquiry()
    {
        VehicleExists();
        _workOrders.OnGetLatestForVehicle = (_, _) => new WorkOrder { OrderId = 9, Status = "en_proceso" };
        var expected = new VehicleStatusInquiry { InquiryId = 7, TenantId = 1, VehicleId = 41, ClientId = 31 };
        _repository.OnCreate = (tenantId, vehicleId, clientId) =>
        {
            Assert.Equal(1, tenantId);
            Assert.Equal(41, vehicleId);
            Assert.Equal(31, clientId);
            return expected;
        };

        var (ok, rateLimited, notFound, notReceived, result) = _service.Create(tenantId: 1, vehicleId: 41, clientId: 31);

        Assert.True(ok);
        Assert.False(rateLimited);
        Assert.False(notFound);
        Assert.False(notReceived);
        Assert.Same(expected, result);
    }

    [Fact]
    public void ListPending_DelegatesToTheRepository()
    {
        var expected = new List<VehicleStatusInquiryRow>
        {
            new(7, 41, 31, "Pedro Elser", "Toyota Hilux", "AH222BB", DateTime.UtcNow),
        };
        _repository.OnListPending = tenantId => { Assert.Equal(1, tenantId); return expected; };

        Assert.Same(expected, _service.ListPending(1));
    }

    [Fact]
    public void Resolve_DelegatesToTheRepository()
    {
        _repository.OnResolve = (tenantId, id) => { Assert.Equal(1, tenantId); Assert.Equal(7, id); return true; };

        Assert.True(_service.Resolve(1, 7));
    }

    private sealed class FakeVehicleStatusInquiryRepository : VehicleStatusInquiryRepository
    {
        public List<string> Calls { get; } = new();
        public Func<int, int, int, VehicleStatusInquiry?> OnCreate { get; set; } =
            (_, _, _) => throw new InvalidOperationException("Unexpected Create");
        public Func<int, List<VehicleStatusInquiryRow>> OnListPending { get; set; } =
            _ => throw new InvalidOperationException("Unexpected ListPending");
        public Func<int, int, bool> OnResolve { get; set; } =
            (_, _) => throw new InvalidOperationException("Unexpected Resolve");

        public override VehicleStatusInquiry? Create(int tenantId, int vehicleId, int clientId)
        {
            Calls.Add(nameof(Create));
            return OnCreate(tenantId, vehicleId, clientId);
        }

        public override List<VehicleStatusInquiryRow> ListPending(int tenantId)
        {
            Calls.Add(nameof(ListPending));
            return OnListPending(tenantId);
        }

        public override bool Resolve(int tenantId, int id)
        {
            Calls.Add(nameof(Resolve));
            return OnResolve(tenantId, id);
        }
    }

    private sealed class FakeVehicleRepository : VehicleRepository
    {
        public Func<int, int, Vehicle?> OnGetById { get; set; } =
            (_, _) => throw new InvalidOperationException("Unexpected GetById");

        public override Vehicle? GetById(int id, int tenantId) => OnGetById(id, tenantId);
    }

    private sealed class FakeWorkOrderRepository : WorkOrderRepository
    {
        public bool Called { get; private set; }
        public Func<int, int, WorkOrder?> OnGetLatestForVehicle { get; set; } =
            (_, _) => throw new InvalidOperationException("Unexpected GetLatestForVehicle");

        public override WorkOrder? GetLatestForVehicle(int vehicleId, int tenantId)
        {
            Called = true;
            return OnGetLatestForVehicle(vehicleId, tenantId);
        }
    }
}
