using FluentAssertions;
using ParcelTrack.TrackingService.Domain.Entities;
using ParcelTrack.TrackingService.Domain.Enums;

namespace ParcelTrack.TrackingService.UnitTests.Domain;

public sealed class TrackedShipmentTests
{
    private static TrackedShipment Create() => TrackedShipment.Create(
        Guid.NewGuid(),
        "DA240101ABCDE",
        CarrierType.Pathao,
        Guid.NewGuid(),
        Guid.NewGuid(),
        "buyer@example.com",
        new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

    private static readonly DateTime Now = new(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ShouldStartActiveInCreatedStatus()
    {
        var shipment = Create();

        shipment.IsActive.Should().BeTrue();
        shipment.LastKnownStatus.Should().Be(CarrierStatus.Created);
        shipment.LastPolledAt.Should().BeNull();
    }

    [Fact]
    public void TryRecordObservedStatus_ShouldReturnTrueOnChange()
    {
        var shipment = Create();

        shipment.TryRecordObservedStatus(CarrierStatus.InTransit, Now).Should().BeTrue();
        shipment.LastKnownStatus.Should().Be(CarrierStatus.InTransit);
    }

    [Fact]
    public void TryRecordObservedStatus_ShouldReturnFalseWhenStatusUnchanged()
    {
        // Polling is repetitive; without this the buyer gets an email every 30 seconds.
        var shipment = Create();
        shipment.TryRecordObservedStatus(CarrierStatus.InTransit, Now);

        shipment.TryRecordObservedStatus(CarrierStatus.InTransit, Now.AddMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void TryRecordObservedStatus_ShouldIgnoreUnknownStatus()
    {
        // An unmapped courier status is not evidence that anything changed.
        var shipment = Create();

        shipment.TryRecordObservedStatus(CarrierStatus.Unknown, Now).Should().BeFalse();
        shipment.LastKnownStatus.Should().Be(CarrierStatus.Created);
    }

    [Fact]
    public void TryRecordObservedStatus_ShouldAlwaysUpdateLastPolledAt()
    {
        // Even an unchanged status counts as polled — that timestamp drives cycle fairness.
        var shipment = Create();

        shipment.TryRecordObservedStatus(CarrierStatus.Unknown, Now);

        shipment.LastPolledAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(CarrierStatus.Delivered)]
    [InlineData(CarrierStatus.Cancelled)]
    [InlineData(CarrierStatus.Returned)]
    public void TryRecordObservedStatus_ShouldDeactivateOnTerminalStatus(CarrierStatus terminal)
    {
        var shipment = Create();

        shipment.TryRecordObservedStatus(terminal, Now);

        shipment.IsActive.Should().BeFalse();
    }

    [Theory]
    [InlineData(CarrierStatus.InTransit)]
    [InlineData(CarrierStatus.OutForDelivery)]
    [InlineData(CarrierStatus.Failed)]
    public void TryRecordObservedStatus_ShouldStayActiveOnNonTerminalStatus(CarrierStatus status)
    {
        var shipment = Create();

        shipment.TryRecordObservedStatus(status, Now);

        shipment.IsActive.Should().BeTrue();
    }

    [Fact]
    public void TryRecordObservedStatus_ShouldAllowFailedThenRetry()
    {
        // A failed delivery is retryable — the courier will try again tomorrow.
        var shipment = Create();
        shipment.TryRecordObservedStatus(CarrierStatus.OutForDelivery, Now);
        shipment.TryRecordObservedStatus(CarrierStatus.Failed, Now.AddHours(2));

        shipment.TryRecordObservedStatus(CarrierStatus.OutForDelivery, Now.AddDays(1)).Should().BeTrue();
        shipment.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SyncStatus_ShouldUpdateWithoutBeingTreatedAsObservation()
    {
        // Used when the poller's own event round-trips back through Kafka.
        var shipment = Create();

        shipment.SyncStatus(CarrierStatus.InTransit, Now);

        shipment.LastKnownStatus.Should().Be(CarrierStatus.InTransit);
        // The next real poll of the same status must then be silent.
        shipment.TryRecordObservedStatus(CarrierStatus.InTransit, Now.AddMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void SyncStatus_ShouldDeactivateOnTerminalStatus()
    {
        // A parcel cancelled through the API must stop being polled.
        var shipment = Create();

        shipment.SyncStatus(CarrierStatus.Cancelled, Now);

        shipment.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SyncStatus_ShouldIgnoreUnknownStatus()
    {
        var shipment = Create();

        shipment.SyncStatus(CarrierStatus.Unknown, Now);

        shipment.LastKnownStatus.Should().Be(CarrierStatus.Created);
    }

    [Fact]
    public void Deactivate_ShouldStopPolling()
    {
        var shipment = Create();

        shipment.Deactivate(Now);

        shipment.IsActive.Should().BeFalse();
    }
}
