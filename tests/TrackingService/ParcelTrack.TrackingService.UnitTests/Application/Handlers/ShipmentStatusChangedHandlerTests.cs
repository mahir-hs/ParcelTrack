using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.Handlers;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.UnitTests.Application.Handlers;

public sealed class ShipmentStatusChangedHandlerTests
{
    private readonly Mock<ITrackingRepository> _repoMock;
    private readonly ShipmentStatusChangedHandler _handler;

    public ShipmentStatusChangedHandlerTests()
    {
        _repoMock = new Mock<ITrackingRepository>();
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<TrackingRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new ShipmentStatusChangedHandler(
            _repoMock.Object,
            Mock.Of<ILogger<ShipmentStatusChangedHandler>>());
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
    public async Task HandleAsync_ShouldMapNewStatusToRecord()
    {
        await _handler.HandleAsync(BuildEvent(newStatus: "Delivered"));

        _repoMock.Verify(
            r => r.AddAsync(
                It.Is<TrackingRecord>(t => t.Status == "Delivered"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldMapLocationFromEvent()
    {
        await _handler.HandleAsync(BuildEvent(location: "Chittagong"));

        _repoMock.Verify(
            r => r.AddAsync(
                It.Is<TrackingRecord>(t => t.Location == "Chittagong"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldMapDescriptionFromEvent()
    {
        await _handler.HandleAsync(BuildEvent(description: "Delivered to front door"));

        _repoMock.Verify(
            r => r.AddAsync(
                It.Is<TrackingRecord>(t => t.Description == "Delivered to front door"),
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

    [Fact]
    public async Task HandleAsync_WithNullLocation_ShouldStoreNullLocation()
    {
        await _handler.HandleAsync(BuildEvent(location: null));

        _repoMock.Verify(
            r => r.AddAsync(
                It.Is<TrackingRecord>(t => t.Location == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ShipmentStatusChangedEvent BuildEvent(
        string newStatus = "InTransit",
        string? location = "Dhaka",
        string description = "Status updated",
        Guid? tenantId = null) =>
        new(
            ShipmentId: Guid.NewGuid(),
            TrackingNumber: "STD-001",
            TenantId: tenantId ?? Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            BuyerEmail: null,
            PreviousStatus: "Created",
            NewStatus: newStatus,
            Location: location,
            Description: description,
            OccurredAt: DateTime.UtcNow);
}
