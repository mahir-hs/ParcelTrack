using FluentAssertions;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.UnitTests.Domain;

public sealed class TrackingRecordTests
{
    [Fact]
    public void Create_ShouldAssignNewId()
    {
        var record = BuildRecord();

        record.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_TwoRecords_ShouldHaveDifferentIds()
    {
        var first = BuildRecord();
        var second = BuildRecord();

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Create_ShouldStoreAllMandatoryFields()
    {
        var shipmentId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var record = TrackingRecord.Create(
            shipmentId,
            trackingNumber: "STD-001",
            status: "InTransit",
            description: "Picked up by carrier",
            carrierType: "Steadfast",
            tenantId,
            occurredAt);

        record.ShipmentId.Should().Be(shipmentId);
        record.TrackingNumber.Should().Be("STD-001");
        record.Status.Should().Be("InTransit");
        record.Description.Should().Be("Picked up by carrier");
        record.CarrierType.Should().Be("Steadfast");
        record.TenantId.Should().Be(tenantId);
        record.OccurredAt.Should().Be(occurredAt);
    }

    [Fact]
    public void Create_WithLocation_ShouldStoreLocation()
    {
        var record = BuildRecord(location: "Dhaka");

        record.Location.Should().Be("Dhaka");
    }

    [Fact]
    public void Create_WithoutLocation_ShouldHaveNullLocation()
    {
        var record = BuildRecord(location: null);

        record.Location.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TrackingRecord BuildRecord(string? location = null) =>
        TrackingRecord.Create(
            shipmentId: Guid.NewGuid(),
            trackingNumber: "STD-001",
            status: "Created",
            description: "Shipment registered",
            carrierType: "Steadfast",
            tenantId: Guid.NewGuid(),
            occurredAt: DateTime.UtcNow,
            location: location);
}
