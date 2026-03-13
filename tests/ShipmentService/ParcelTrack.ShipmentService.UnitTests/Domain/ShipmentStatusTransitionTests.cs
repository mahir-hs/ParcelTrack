using FluentAssertions;
using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Enums;
using ParcelTrack.ShipmentService.Domain.Exceptions;

namespace ParcelTrack.ShipmentService.UnitTests.Domain;

public sealed class ShipmentStatusTransitionTests
{
    private static Shipment CreateShipment() =>
        Shipment.Create("TRK001", CarrierType.Steadfast, "buyer@test.com", Guid.NewGuid(), Guid.NewGuid());

    // ── Valid transitions ──────────────────────────────────────────────────

    [Fact]
    public void UpdateStatus_CreatedToInTransit_ShouldSucceed()
    {
        var shipment = CreateShipment();

        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");

        shipment.Status.Should().Be(ShipmentStatus.InTransit);
    }

    [Fact]
    public void UpdateStatus_InTransitToOutForDelivery_ShouldSucceed()
    {
        var shipment = CreateShipment();
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");

        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Out for delivery", "Chattogram");

        shipment.Status.Should().Be(ShipmentStatus.OutForDelivery);
    }

    [Fact]
    public void UpdateStatus_OutForDeliveryToDelivered_ShouldSucceed()
    {
        var shipment = CreateShipment();
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");
        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Out for delivery", "Chattogram");

        shipment.UpdateStatus(ShipmentStatus.Delivered, "Delivered", "Chattogram");

        shipment.Status.Should().Be(ShipmentStatus.Delivered);
    }

    [Fact]
    public void UpdateStatus_OutForDeliveryToFailed_ShouldSucceed()
    {
        var shipment = CreateShipment();
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");
        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Out for delivery", "Chattogram");

        shipment.UpdateStatus(ShipmentStatus.Failed, "Customer not available", "Chattogram");

        shipment.Status.Should().Be(ShipmentStatus.Failed);
    }

    [Fact]
    public void UpdateStatus_FailedToOutForDelivery_ShouldSucceedAsRetry()
    {
        var shipment = CreateShipment();
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");
        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Out for delivery", "Chattogram");
        shipment.UpdateStatus(ShipmentStatus.Failed, "Customer not available", "Chattogram");

        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Second attempt", "Chattogram");

        shipment.Status.Should().Be(ShipmentStatus.OutForDelivery);
    }

    [Fact]
    public void UpdateStatus_ShouldRecordEventOnEveryTransition()
    {
        var shipment = CreateShipment();
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");
        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Out for delivery", "Chattogram");
        shipment.UpdateStatus(ShipmentStatus.Delivered, "Delivered", "Chattogram");

        // 1 (Created) + 3 transitions = 4 events
        shipment.Events.Should().HaveCount(4);
    }

    // ── Invalid transitions ────────────────────────────────────────────────

    [Fact]
    public void UpdateStatus_CreatedToDelivered_ShouldThrowInvalidShipmentStatusTransitionException()
    {
        var shipment = CreateShipment();

        var act = () => shipment.UpdateStatus(ShipmentStatus.Delivered, "Skip ahead", "Dhaka");

        act.Should().Throw<InvalidShipmentStatusTransitionException>()
            .Which.From.Should().Be(ShipmentStatus.Created);
    }

    [Fact]
    public void UpdateStatus_CreatedToFailed_ShouldThrowInvalidShipmentStatusTransitionException()
    {
        var shipment = CreateShipment();

        var act = () => shipment.UpdateStatus(ShipmentStatus.Failed, "Skip ahead", "Dhaka");

        act.Should().Throw<InvalidShipmentStatusTransitionException>()
            .Which.From.Should().Be(ShipmentStatus.Created);
    }

    [Fact]
    public void UpdateStatus_InTransitToDelivered_ShouldThrowInvalidShipmentStatusTransitionException()
    {
        var shipment = CreateShipment();
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");

        var act = () => shipment.UpdateStatus(ShipmentStatus.Delivered, "Skip ahead", "Dhaka");

        act.Should().Throw<InvalidShipmentStatusTransitionException>()
            .Which.From.Should().Be(ShipmentStatus.InTransit);
    }

    // ── Terminal state protection ──────────────────────────────────────────

    [Fact]
    public void UpdateStatus_WhenDelivered_ShouldThrowShipmentAlreadyTerminatedException()
    {
        var shipment = CreateShipment();
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");
        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Out for delivery", "Chattogram");
        shipment.UpdateStatus(ShipmentStatus.Delivered, "Delivered", "Chattogram");

        var act = () => shipment.UpdateStatus(ShipmentStatus.InTransit, "Somehow back?", "Dhaka");

        act.Should().Throw<ShipmentAlreadyTerminatedException>()
            .Which.CurrentStatus.Should().Be(ShipmentStatus.Delivered);
    }

    [Fact]
    public void UpdateStatus_WhenCancelled_ShouldThrowShipmentAlreadyTerminatedException()
    {
        var shipment = CreateShipment();
        shipment.Cancel("Buyer requested cancellation");

        var act = () => shipment.UpdateStatus(ShipmentStatus.InTransit, "Too late", "Dhaka");

        act.Should().Throw<ShipmentAlreadyTerminatedException>()
            .Which.CurrentStatus.Should().Be(ShipmentStatus.Cancelled);
    }
}