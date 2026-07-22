using FluentAssertions;
using Moq;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.Handlers;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.UnitTests.Application.Handlers;

public sealed class ShipmentCreatedEventHandlerTests
{
    private readonly Mock<ITrackingRepository> _repoMock;
    private readonly ShipmentCreatedEventHandler _handler;

    public ShipmentCreatedEventHandlerTests()
    {
        _repoMock = new Mock<ITrackingRepository>();
        _handler = new ShipmentCreatedEventHandler(_repoMock.Object);
    }

    private static ShipmentCreatedEvent BuildEvent() => new(
        ShipmentId: Guid.NewGuid(),
        TrackingNumber: "TRK-98765",
        CarrierType: "Steadfast",
        UserId: Guid.NewGuid(),
        TenantId: Guid.NewGuid(),
        BuyerEmail: "buyer@test.com",
        CreatedAt: DateTime.UtcNow);

    [Fact]
    public async Task Handle_WhenRecordDoesNotExist_CreatesAndPersistsTrackingRecord()
    {
        // Arrange
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

        // Assert
        added.Should().NotBeNull();
        added!.ShipmentId.Should().Be(e.ShipmentId);
        added.TrackingNumber.Should().Be(e.TrackingNumber);
        added.TenantId.Should().Be(e.TenantId);
        added.CarrierType.Should().Be(e.CarrierType);
        added.CurrentStatus.Should().Be("Created");
    }

    [Fact]
    public async Task Handle_WhenRecordDoesNotExist_CallsSaveChangesOnce()
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
        _repoMock.Verify(r => r.AddAsync(It.IsAny<TrackingRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRecordAlreadyExists_IsIdempotentAndDoesNotCreate()
    {
        // Arrange: a duplicate create event arrives after the record already exists
        var e = BuildEvent();
        var existing = TrackingRecord.Create(e.ShipmentId, e.TrackingNumber, e.TenantId, e.CarrierType);
        _repoMock
            .Setup(r => r.GetByShipmentIdAsync(e.ShipmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        await _handler.Handle(e, CancellationToken.None);

        // Assert: no new record is added, no save happens
        _repoMock.Verify(r => r.AddAsync(It.IsAny<TrackingRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
