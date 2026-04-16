using FluentAssertions;
using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Enums;
using ParcelTrack.ShipmentService.Domain.Exceptions;

namespace ParcelTrack.ShipmentService.UnitTests.Domain;

public sealed class ShipmentDeliveryAttemptsTests
{
    private static Shipment CreateInTransitShipment()
    {
        var shipment = Shipment.Create("TRK001", CarrierType.Steadfast, "buyer@test.com", Guid.NewGuid(), Guid.NewGuid());
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");
        return shipment;
    }

    [Fact]
    public void UpdateStatus_FirstOutForDelivery_ShouldIncrementAttemptsToOne()
    {
        var shipment = CreateInTransitShipment();

        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "First attempt", "Chattogram");

        shipment.DeliveryAttempts.Should().Be(1);
    }

    [Fact]
    public void UpdateStatus_IncreasesDeliveryAttempts_OnEachOutForDelivery()
    {
        var shipment = CreateInTransitShipment();
        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "First attempt", "Chattogram");
        shipment.UpdateStatus(ShipmentStatus.Failed, "Not available", "Chattogram");
        shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Second attempt", "Chattogram");

        shipment.DeliveryAttempts.Should().Be(2);
    }

    [Fact]
    public void UpdateStatus_ThirdDeliveryAttempt_ShouldStillSucceed()
    {
        var shipment = CreateInTransitShipment();

        for (var i = 1; i <= 2; i++)
        {
            shipment.UpdateStatus(ShipmentStatus.OutForDelivery, $"Attempt {i}", "Chattogram");
            shipment.UpdateStatus(ShipmentStatus.Failed, "Customer not available", "Chattogram");
        }

        // 3rd attempt — must still be allowed
        var act = () => shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Attempt 3", "Chattogram");

        act.Should().NotThrow();
        shipment.DeliveryAttempts.Should().Be(3);
    }

    [Fact]
    public void UpdateStatus_FourthDeliveryAttempt_ShouldThrowMaxDeliveryAttemptsExceededException()
    {
        var shipment = CreateInTransitShipment();

        // 3 attempts — all allowed
        for (var i = 1; i <= 3; i++)
        {
            shipment.UpdateStatus(ShipmentStatus.OutForDelivery, $"Attempt {i}", "Chattogram");
            if (i < 3)
                shipment.UpdateStatus(ShipmentStatus.Failed, "Customer not available", "Chattogram");
        }

        // 4th attempt — must throw
        shipment.UpdateStatus(ShipmentStatus.Failed, "Failed again", "Chattogram");
        var act = () => shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Attempt 4", "Chattogram");

        act.Should().Throw<MaxDeliveryAttemptsExceededException>()
            .Which.ShipmentId.Should().Be(shipment.Id);
    }

    [Fact]
    public void UpdateStatus_FourthDeliveryAttempt_ExceptionShouldContainCorrectAttemptCounts()
    {
        var shipment = CreateInTransitShipment();

        for (var i = 1; i <= 3; i++)
        {
            shipment.UpdateStatus(ShipmentStatus.OutForDelivery, $"Attempt {i}", "Chattogram");
            if (i < 3)
                shipment.UpdateStatus(ShipmentStatus.Failed, "Customer not available", "Chattogram");
        }

        shipment.UpdateStatus(ShipmentStatus.Failed, "Failed again", "Chattogram");
        var act = () => shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Attempt 4", "Chattogram");

        act.Should().Throw<MaxDeliveryAttemptsExceededException>()
            .Which.CurrentAttempts.Should().Be(3);
    }

    [Fact]
    public void UpdateStatus_NonOutForDeliveryTransition_ShouldNotIncrementAttempts()
    {
        // InTransit alone should not count as a delivery attempt
        var shipment = Shipment.Create("TRK001", CarrierType.Steadfast, null, Guid.NewGuid(), Guid.NewGuid());
        shipment.UpdateStatus(ShipmentStatus.InTransit, "Picked up", "Dhaka");

        shipment.DeliveryAttempts.Should().Be(0);
    }
}