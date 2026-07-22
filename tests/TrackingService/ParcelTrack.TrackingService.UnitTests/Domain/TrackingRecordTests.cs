using FluentAssertions;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.UnitTests.Domain;

public sealed class TrackingRecordTests
{
    private static readonly Guid ShipmentId = Guid.NewGuid();
    private const string TrackingNumber = "TRK-12345";
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string CarrierType = "Steadfast";

    private static TrackingRecord CreateRecord() =>
        TrackingRecord.Create(ShipmentId, TrackingNumber, TenantId, CarrierType);

    // ── Initial state ──────────────────────────────────────────────────────

    [Fact]
    public void Create_NewRecord_HasExpectedInitialState()
    {
        var record = CreateRecord();

        record.ShipmentId.Should().Be(ShipmentId);
        record.TrackingNumber.Should().Be(TrackingNumber);
        record.TenantId.Should().Be(TenantId);
        record.CarrierType.Should().Be(CarrierType);
        record.CurrentStatus.Should().Be("Created");
        record.LastLocation.Should().BeNull();
        record.LastEventAt.Should().NotBeNull();
    }

    [Fact]
    public void Create_NewRecord_SeedsASingleCreatedEvent()
    {
        var record = CreateRecord();

        record.Events.Should().HaveCount(1);
        var first = record.Events.Single();
        first.Status.Should().Be("Created");
        first.Description.Should().Be("Shipment registered for tracking.");
        first.Location.Should().BeNull();
        first.ShipmentId.Should().Be(ShipmentId);
    }

    // ── Status change behaviour ────────────────────────────────────────────

    [Fact]
    public void ApplyStatusChange_UpdatesCurrentStatus()
    {
        var record = CreateRecord();

        record.ApplyStatusChange("InTransit", "Dhaka", "Picked up", DateTime.UtcNow);

        record.CurrentStatus.Should().Be("InTransit");
    }

    [Fact]
    public void ApplyStatusChange_UpdatesLastLocation()
    {
        var record = CreateRecord();

        record.ApplyStatusChange("InTransit", "Dhaka", "Picked up", DateTime.UtcNow);

        record.LastLocation.Should().Be("Dhaka");
    }

    [Fact]
    public void ApplyStatusChange_AppendsANewEvent()
    {
        var record = CreateRecord();

        record.ApplyStatusChange("InTransit", "Dhaka", "Picked up", DateTime.UtcNow);

        // 1 (Created) + 1 transition = 2 events
        record.Events.Should().HaveCount(2);
        var latest = record.Events.Last();
        latest.Status.Should().Be("InTransit");
        latest.Description.Should().Be("Picked up");
        latest.Location.Should().Be("Dhaka");
        latest.ShipmentId.Should().Be(ShipmentId);
    }

    [Fact]
    public void ApplyStatusChange_MultipleTransitions_RecordsEveryEvent()
    {
        var record = CreateRecord();
        record.ApplyStatusChange("InTransit", "Dhaka", "Picked up", DateTime.UtcNow);
        record.ApplyStatusChange("OutForDelivery", "Chattogram", "Out for delivery", DateTime.UtcNow);
        record.ApplyStatusChange("Delivered", "Chattogram", "Delivered", DateTime.UtcNow);

        // 1 (Created) + 3 transitions = 4 events
        record.Events.Should().HaveCount(4);
        record.CurrentStatus.Should().Be("Delivered");
    }

    [Fact]
    public void ApplyStatusChange_WithNullLocation_ClearsLastLocation()
    {
        var record = CreateRecord();
        record.ApplyStatusChange("InTransit", "Dhaka", "Picked up", DateTime.UtcNow);

        record.ApplyStatusChange("OutForDelivery", null, "Left hub", DateTime.UtcNow);

        record.LastLocation.Should().BeNull();
    }

    // ── LastEventAt bookkeeping ────────────────────────────────────────────

    [Fact]
    public void ApplyStatusChange_NewerOccurredAt_UpdatesLastEventAt()
    {
        var record = CreateRecord();
        var createdAt = record.LastEventAt!.Value;
        var older = createdAt.AddMinutes(-10);

        record.ApplyStatusChange("InTransit", "Dhaka", "Picked up", older);

        var newer = createdAt.AddMinutes(10);
        record.ApplyStatusChange("OutForDelivery", "Chattogram", "Out for delivery", newer);

        record.LastEventAt.Should().Be(newer);
    }

    [Fact]
    public void ApplyStatusChange_OlderOccurredAt_DoesNotMoveLastEventAtBackwards()
    {
        var record = CreateRecord();
        var createdAt = record.LastEventAt!.Value;
        var newer = createdAt.AddMinutes(10);

        record.ApplyStatusChange("InTransit", "Dhaka", "Picked up", newer);

        var older = createdAt.AddMinutes(5);
        record.ApplyStatusChange("OutForDelivery", "Chattogram", "Out for delivery", older);

        record.LastEventAt.Should().Be(newer);
    }
}
