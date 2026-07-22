using FluentAssertions;
using Moq;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.Handlers;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.UnitTests.Application.Handlers;

public sealed class ShipmentStatusChangedEventHandlerTests
{
    private readonly Mock<ITrackingRepository> _repoMock;
    private readonly ShipmentStatusChangedEventHandler _handler;

    public ShipmentStatusChangedEventHandlerTests()
    {
        _repoMock = new Mock<ITrackingRepository>();
        _handler = new ShipmentStatusChangedEventHandler(_repoMock.Object);
    }

    private static ShipmentStatusChangedEvent BuildEvent(Guid? shipmentId = null) => new(
        ShipmentId: shipmentId ?? Guid.NewGuid(),
        TrackingNumber: "TRK-98765",
        TenantId: Guid.NewGuid(),
        UserId: Guid.NewGuid(),
        PreviousStatus: "Created",
        NewStatus: "InTransit",
        Location: "Dhaka",
        Description: "Picked up",
        OccurredAt: DateTime.UtcNow);

    [Fact]
    public async Task Handle_WhenRecordExists_AppendsTrackingEventAndUpdatesStatus()
    {
        // Arrange
        var e = BuildEvent();
        var record = TrackingRecord.Create(e.ShipmentId, e.TrackingNumber, e.TenantId, "Steadfast");
        _repoMock
            .Setup(r => r.GetByShipmentIdAsync(e.ShipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        // Act
        await _handler.Handle(e, CancellationToken.None);

        // Assert
        record.CurrentStatus.Should().Be("InTransit");
        record.LastLocation.Should().Be("Dhaka");
        // 1 (Created) + 1 transition = 2 events
        record.Events.Should().HaveCount(2);
        record.Events.Last().Status.Should().Be("InTransit");
        record.Events.Last().Description.Should().Be("Picked up");
    }

    [Fact]
    public async Task Handle_WhenRecordExists_DoesNotAddANewRecord()
    {
        // Arrange
        var e = BuildEvent();
        var record = TrackingRecord.Create(e.ShipmentId, e.TrackingNumber, e.TenantId, "Steadfast");
        _repoMock
            .Setup(r => r.GetByShipmentIdAsync(e.ShipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        // Act
        await _handler.Handle(e, CancellationToken.None);

        // Assert
        _repoMock.Verify(r => r.AddAsync(It.IsAny<TrackingRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRecordDoesNotExist_CreatesRecordThenAppendsEvent()
    {
        // Arrange: status.changed arrived before the create event
        var e = BuildEvent();
        _repoMock
            .Setup(r => r.GetByShipmentIdAsync(e.ShipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackingRecord?)null);

        TrackingRecord? added = null;
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<TrackingRecord>(), It.IsAny<CancellationToken>()))
            .Callback<TrackingRecord, CancellationToken>((r, _) => added = r);

        // Act
        await _handler.Handle(e, CancellationToken.None);

        // Assert: an on-demand record is created with the "Unknown" carrier fallback
        added.Should().NotBeNull();
        added!.ShipmentId.Should().Be(e.ShipmentId);
        added.CarrierType.Should().Be("Unknown");
        // created record seeds 1 Created event + the transition = 2 events
        added.Events.Should().HaveCount(2);
        added.CurrentStatus.Should().Be("InTransit");
    }

    [Fact]
    public async Task Handle_Always_CallsSaveChangesOnce()
    {
        // Arrange
        var e = BuildEvent();
        _repoMock
            .Setup(r => r.GetByShipmentIdAsync(e.ShipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackingRecord?)null);

        // Act
        await _handler.Handle(e, CancellationToken.None);

        // Assert
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
