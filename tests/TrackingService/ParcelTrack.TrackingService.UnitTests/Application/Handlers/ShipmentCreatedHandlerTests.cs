using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.Handlers;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.UnitTests.Application.Handlers;

public sealed class ShipmentCreatedHandlerTests
{
    private readonly Mock<ITrackingRepository> _repoMock;
    private readonly Mock<ITrackedShipmentRepository> _trackedMock;
    private readonly ShipmentCreatedHandler _handler;

    public ShipmentCreatedHandlerTests()
    {
        _repoMock = new Mock<ITrackingRepository>();
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<TrackingRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _trackedMock = new Mock<ITrackedShipmentRepository>();

        _handler = new ShipmentCreatedHandler(
            _repoMock.Object,
            _trackedMock.Object,
            Mock.Of<ILogger<ShipmentCreatedHandler>>());
    }

    [Fact]
    public async Task HandleAsync_ShouldCallRepositoryAddOnce()
    {
        await _handler.HandleAsync(BuildEvent());

        _repoMock.Verify(
            r => r.AddAsync(It.IsAny<TrackingRecord>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldCreateRecordWithCreatedStatus()
    {
        await _handler.HandleAsync(BuildEvent());

        _repoMock.Verify(
            r => r.AddAsync(
                It.Is<TrackingRecord>(t => t.Status == "Created"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldMapShipmentIdFromEvent()
    {
        var shipmentId = Guid.NewGuid();

        await _handler.HandleAsync(BuildEvent(shipmentId: shipmentId));

        _repoMock.Verify(
            r => r.AddAsync(
                It.Is<TrackingRecord>(t => t.ShipmentId == shipmentId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldMapTrackingNumberFromEvent()
    {
        await _handler.HandleAsync(BuildEvent(trackingNumber: "STD-XYZ"));

        _repoMock.Verify(
            r => r.AddAsync(
                It.Is<TrackingRecord>(t => t.TrackingNumber == "STD-XYZ"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldMapCarrierTypeFromEvent()
    {
        await _handler.HandleAsync(BuildEvent(carrierType: "Pathao"));

        _repoMock.Verify(
            r => r.AddAsync(
                It.Is<TrackingRecord>(t => t.CarrierType == "Pathao"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldMapTenantIdFromEvent()
    {
        var tenantId = Guid.NewGuid();

        await _handler.HandleAsync(BuildEvent(tenantId: tenantId));

        _repoMock.Verify(
            r => r.AddAsync(
                It.Is<TrackingRecord>(t => t.TenantId == tenantId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ShipmentCreatedEvent BuildEvent(
        Guid? shipmentId = null,
        string trackingNumber = "STD-001",
        string carrierType = "Steadfast",
        Guid? tenantId = null) =>
        new(
            ShipmentId: shipmentId ?? Guid.NewGuid(),
            TrackingNumber: trackingNumber,
            CarrierType: carrierType,
            UserId: Guid.NewGuid(),
            TenantId: tenantId ?? Guid.NewGuid(),
            BuyerEmail: null,
            CreatedAt: DateTime.UtcNow);
}
