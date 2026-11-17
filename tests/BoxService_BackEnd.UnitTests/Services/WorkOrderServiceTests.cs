using System;
using System.Collections.Generic;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Repositories;
using BoxService_BackEnd.Services;
using Xunit;

namespace BoxService_BackEnd.UnitTests.Services;

public class WorkOrderServiceTests
{
    private readonly FakeWorkOrderRepository _repository = new();
    private readonly FakeVehicleRepository _vehicles = new();
    private readonly WorkOrderService _service;

    public WorkOrderServiceTests() => _service = new WorkOrderService(_repository, _vehicles);

    [Fact]
    public void Receive_WhenVehicleDoesNotExist_ReturnsNotFound_AndNeverHitsTheRepository()
    {
        _vehicles.OnGetById = (_, _) => null;

        var (ok, notFound, alreadyOpen, result) = _service.Receive(1, 41, null, "tenant1");

        Assert.False(ok);
        Assert.True(notFound);
        Assert.False(alreadyOpen);
        Assert.Null(result);
        Assert.Empty(_repository.Calls);
    }

    [Fact]
    public void Receive_WhenRepositoryDeclinesTheInsert_ReturnsAlreadyOpen()
    {
        _vehicles.OnGetById = (_, _) => new Vehicle { VehicleId = 41, TenantId = 1 };
        _repository.OnCreate = (_, _, _, _) => null;

        var (ok, notFound, alreadyOpen, result) = _service.Receive(1, 41, null, "tenant1");

        Assert.False(ok);
        Assert.False(notFound);
        Assert.True(alreadyOpen);
        Assert.Null(result);
    }

    [Fact]
    public void Receive_WhenValid_ReturnsOkWithTheCreatedOrder()
    {
        _vehicles.OnGetById = (_, _) => new Vehicle { VehicleId = 41, TenantId = 1 };
        var expected = new WorkOrder { OrderId = 9, TenantId = 1, VehicleId = 41, Status = "en_espera" };
        _repository.OnCreate = (tenantId, vehicleId, notes, receivedBy) =>
        {
            Assert.Equal(1, tenantId);
            Assert.Equal(41, vehicleId);
            Assert.Equal("tenant1", receivedBy);
            return expected;
        };

        var (ok, notFound, alreadyOpen, result) = _service.Receive(1, 41, null, "tenant1");

        Assert.True(ok);
        Assert.False(notFound);
        Assert.False(alreadyOpen);
        Assert.Same(expected, result);
    }

    [Fact]
    public void Advance_WhenOrderDoesNotExist_ReturnsNotFound()
    {
        _repository.OnGetById = (_, _) => null;

        var (ok, notFound, invalidTransition) = _service.Advance(1, 9, "en_proceso", null, null);

        Assert.False(ok);
        Assert.True(notFound);
        Assert.False(invalidTransition);
        Assert.Empty(_repository.UpdateStatusCalls);
    }

    [Fact]
    public void Advance_WhenSkippingAStep_ReturnsInvalidTransition_AndNeverCallsUpdateStatus()
    {
        _repository.OnGetById = (_, _) => new WorkOrder { OrderId = 9, Status = "en_espera" };

        var (ok, notFound, invalidTransition) = _service.Advance(1, 9, "terminado", null, null);

        Assert.False(ok);
        Assert.False(notFound);
        Assert.True(invalidTransition);
        Assert.Empty(_repository.UpdateStatusCalls);
    }

    [Fact]
    public void Advance_WhenItLosesTheRace_ReturnsInvalidTransition()
    {
        _repository.OnGetById = (_, _) => new WorkOrder { OrderId = 9, Status = "en_espera" };
        _repository.OnUpdateStatus = (_, _, _, _, _, _) => false;

        var (ok, notFound, invalidTransition) = _service.Advance(1, 9, "en_proceso", null, null);

        Assert.False(ok);
        Assert.False(notFound);
        Assert.True(invalidTransition);
    }

    [Fact]
    public void Advance_WhenValid_ReturnsOk_AndForwardsExpectedCurrentStatus()
    {
        _repository.OnGetById = (_, _) => new WorkOrder { OrderId = 9, Status = "en_proceso" };
        _repository.OnUpdateStatus = (id, tenant, current, target, notes, deliveredBy) =>
        {
            Assert.Equal(9, id);
            Assert.Equal(1, tenant);
            Assert.Equal("en_proceso", current);
            Assert.Equal("terminado", target);
            return true;
        };

        var (ok, notFound, invalidTransition) = _service.Advance(1, 9, "terminado", null, null);

        Assert.True(ok);
        Assert.False(notFound);
        Assert.False(invalidTransition);
    }

    [Fact]
    public void Cancel_WhenStillWaiting_ReturnsOk()
    {
        _repository.OnGetById = (_, _) => new WorkOrder { OrderId = 9, Status = "en_espera" };
        _repository.OnCancel = (_, _) => true;

        var (ok, notFound, invalidState) = _service.Cancel(1, 9);

        Assert.True(ok);
        Assert.False(notFound);
        Assert.False(invalidState);
    }

    [Fact]
    public void Cancel_WhenAlreadyInProgress_ReturnsInvalidState_AndNeverCallsCancel()
    {
        _repository.OnGetById = (_, _) => new WorkOrder { OrderId = 9, Status = "en_proceso" };

        var (ok, notFound, invalidState) = _service.Cancel(1, 9);

        Assert.False(ok);
        Assert.False(notFound);
        Assert.True(invalidState);
        Assert.Empty(_repository.CancelCalls);
    }

    private sealed class FakeWorkOrderRepository : WorkOrderRepository
    {
        public List<string> Calls { get; } = new();
        public List<(int Id, int Tenant, string Current, string Target, string? Notes, string? DeliveredBy)> UpdateStatusCalls { get; } = new();
        public List<(int Id, int Tenant)> CancelCalls { get; } = new();

        public Func<int, int, string?, string, WorkOrder?> OnCreate { get; set; } =
            (_, _, _, _) => throw new InvalidOperationException("Unexpected Create");
        public Func<int, int, WorkOrder?> OnGetById { get; set; } =
            (_, _) => throw new InvalidOperationException("Unexpected GetById");
        public Func<int, int, string, string, string?, string?, bool> OnUpdateStatus { get; set; } =
            (_, _, _, _, _, _) => throw new InvalidOperationException("Unexpected UpdateStatus");
        public Func<int, int, bool> OnCancel { get; set; } =
            (_, _) => throw new InvalidOperationException("Unexpected Cancel");

        public override WorkOrder? Create(int tenantId, int vehicleId, string? notes, string receivedBy)
        {
            Calls.Add(nameof(Create));
            return OnCreate(tenantId, vehicleId, notes, receivedBy);
        }

        public override WorkOrder? GetById(int id, int tenantId)
        {
            Calls.Add(nameof(GetById));
            return OnGetById(id, tenantId);
        }

        public override bool UpdateStatus(int id, int tenantId, string currentStatus, string newStatus, string? notes, string? deliveredBy)
        {
            UpdateStatusCalls.Add((id, tenantId, currentStatus, newStatus, notes, deliveredBy));
            return OnUpdateStatus(id, tenantId, currentStatus, newStatus, notes, deliveredBy);
        }

        public override bool Cancel(int id, int tenantId)
        {
            CancelCalls.Add((id, tenantId));
            return OnCancel(id, tenantId);
        }
    }

    private sealed class FakeVehicleRepository : VehicleRepository
    {
        public Func<int, int, Vehicle?> OnGetById { get; set; } =
            (_, _) => throw new InvalidOperationException("Unexpected GetById");

        public override Vehicle? GetById(int id, int tenantId) => OnGetById(id, tenantId);
    }
}
