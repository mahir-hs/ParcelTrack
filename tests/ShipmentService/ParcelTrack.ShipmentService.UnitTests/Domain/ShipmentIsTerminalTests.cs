using FluentAssertions;
using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.UnitTests.Domain;

public sealed class ShipmentIsTerminalTests
{
    private static Shipment CreateShipment() =>
        Shipment.Create("TRK001", CarrierType.Steadfast, "buyer@test.com", Guid.NewGuid(), Guid.NewGuid());

    // ── Terminal states ────────────────────────────────────────────────────

    [Fact]
    public void IsTerminal_WhenDelivered_ShouldBeTrue()
    {
        var shipment = CreateShipment();
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");
        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Out for delivery", "Chattogram");
        shipment.UpdateStatus(ShipmentStatus.Delivered, "Delivered", "Chattogram");

        shipment.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsTerminal_WhenCancelled_ShouldBeTrue()
    {
        var shipment = CreateShipment();
        shipment.Cancel("Cancelled");

        shipment.IsTerminal.Should().BeTrue();
    }

    // ── Non-terminal states ────────────────────────────────────────────────

    [Fact]
    public void IsTerminal_WhenCreated_ShouldBeFalse()
    {
        var shipment = CreateShipment();

        shipment.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void IsTerminal_WhenInTransit_ShouldBeFalse()
    {
        var shipment = CreateShipment();
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");

        shipment.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void IsTerminal_WhenOutForDelivery_ShouldBeFalse()
    {
        var shipment = CreateShipment();
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");
        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Out for delivery", "Chattogram");

        shipment.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void IsTerminal_WhenFailed_ShouldBeFalse()
    {
        // Failed is NOT terminal — retry is still possible
        var shipment = CreateShipment();
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");
        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Out for delivery", "Chattogram");
        shipment.UpdateStatus(ShipmentStatus.Failed, "Customer not available", "Chattogram");

        shipment.IsTerminal.Should().BeFalse();
    }
}