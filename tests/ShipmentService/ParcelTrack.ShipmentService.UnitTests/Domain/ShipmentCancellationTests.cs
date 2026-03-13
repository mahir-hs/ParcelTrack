using FluentAssertions;
using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Enums;
using ParcelTrack.ShipmentService.Domain.Exceptions;

namespace ParcelTrack.ShipmentService.UnitTests.Domain;

public sealed class ShipmentCancellationTests
{
    private static Shipment CreateShipment() =>
        Shipment.Create("TRK001", CarrierType.Steadfast, "buyer@test.com", Guid.NewGuid(), Guid.NewGuid());

    // ── Valid cancellations ────────────────────────────────────────────────

    [Fact]
    public void Cancel_FromCreated_ShouldSucceed()
    {
        var shipment = CreateShipment();

        shipment.Cancel("Buyer changed mind");

        shipment.Status.Should().Be(ShipmentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromInTransit_ShouldSucceed()
    {
        var shipment = CreateShipment();
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");

        shipment.Cancel("Seller cancelled");

        shipment.Status.Should().Be(ShipmentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldRecordCancellationEvent()
    {
        var shipment = CreateShipment();

        shipment.Cancel("Buyer changed mind");

        shipment.Events.Should().HaveCount(2);
        shipment.Events.Last().Status.Should().Be(ShipmentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldMarkShipmentAsTerminal()
    {
        var shipment = CreateShipment();

        shipment.Cancel("Cancelled");

        shipment.IsTerminal.Should().BeTrue();
    }

    // ── Invalid cancellations ──────────────────────────────────────────────

    [Fact]
    public void Cancel_AlreadyDelivered_ShouldThrowShipmentAlreadyTerminatedException()
    {
        var shipment = CreateShipment();
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");
        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Out for delivery", "Chattogram");
        shipment.UpdateStatus(ShipmentStatus.Delivered, "Delivered", "Chattogram");

        var act = () => shipment.Cancel("Too late");

        act.Should().Throw<ShipmentAlreadyTerminatedException>()
            .Which.CurrentStatus.Should().Be(ShipmentStatus.Delivered);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_ShouldThrowShipmentAlreadyTerminatedException()
    {
        var shipment = CreateShipment();
        shipment.Cancel("First cancellation");

        var act = () => shipment.Cancel("Second cancellation");

        act.Should().Throw<ShipmentAlreadyTerminatedException>()
            .Which.CurrentStatus.Should().Be(ShipmentStatus.Cancelled);
    }
}