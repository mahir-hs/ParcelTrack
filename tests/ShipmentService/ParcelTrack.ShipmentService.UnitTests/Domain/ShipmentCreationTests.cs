using FluentAssertions;
using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.UnitTests.Domain;

public sealed class ShipmentCreationTests
{
    [Fact]
    public void Create_ShouldInitialiseWithCreatedStatus()
    {
        var shipment = Shipment.Create("TRK001", CarrierType.Steadfast, "buyer@test.com", Guid.NewGuid(), Guid.NewGuid());

        shipment.Status.Should().Be(ShipmentStatus.Created);
    }

    [Fact]
    public void Create_ShouldAssignNewId()
    {
        var shipment = Shipment.Create("TRK001", CarrierType.Steadfast, "buyer@test.com", Guid.NewGuid(), Guid.NewGuid());

        shipment.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldRecordInitialEvent()
    {
        var shipment = Shipment.Create("TRK001", CarrierType.Steadfast, "buyer@test.com", Guid.NewGuid(), Guid.NewGuid());

        shipment.Events.Should().HaveCount(1);
        shipment.Events.First().Status.Should().Be(ShipmentStatus.Created);
    }

    [Fact]
    public void Create_ShouldInitialiseDeliveryAttemptsToZero()
    {
        var shipment = Shipment.Create("TRK001", CarrierType.Steadfast, "buyer@test.com", Guid.NewGuid(), Guid.NewGuid());

        shipment.DeliveryAttempts.Should().Be(0);
    }

    [Fact]
    public void Create_ShouldStoreCorrectTrackingNumber()
    {
        var shipment = Shipment.Create("STD-UNIQUE-001", CarrierType.Steadfast, null, Guid.NewGuid(), Guid.NewGuid());

        shipment.TrackingNumber.Should().Be("STD-UNIQUE-001");
    }

    [Fact]
    public void Create_ShouldStoreCorrectTenantId()
    {
        var tenantId = Guid.NewGuid();
        var shipment = Shipment.Create("TRK001", CarrierType.Steadfast, null, Guid.NewGuid(), tenantId);

        shipment.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void Create_WithNullBuyerEmail_ShouldSucceed()
    {
        // B2B tenants don't always provide a buyer email
        var act = () => Shipment.Create("TRK001", CarrierType.Pathao, null, Guid.NewGuid(), Guid.NewGuid());

        act.Should().NotThrow();
    }

    [Fact]
    public void Create_TwoShipments_ShouldHaveDifferentIds()
    {
        var first = Shipment.Create("TRK001", CarrierType.Steadfast, null, Guid.NewGuid(), Guid.NewGuid());
        var second = Shipment.Create("TRK002", CarrierType.Steadfast, null, Guid.NewGuid(), Guid.NewGuid());

        first.Id.Should().NotBe(second.Id);
    }
}