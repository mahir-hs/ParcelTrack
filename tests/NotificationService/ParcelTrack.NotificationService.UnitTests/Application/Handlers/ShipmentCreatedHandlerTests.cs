using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParcelTrack.NotificationService.Application.DTOs;
using ParcelTrack.NotificationService.Application.Handlers;
using ParcelTrack.NotificationService.Application.Interfaces;
using ParcelTrack.Shared.Contracts.Events;

namespace ParcelTrack.NotificationService.UnitTests.Application.Handlers;

public sealed class ShipmentCreatedHandlerTests
{
    private readonly Mock<INotificationSender> _senderMock;
    private readonly ShipmentCreatedHandler _handler;

    public ShipmentCreatedHandlerTests()
    {
        _senderMock = new Mock<INotificationSender>();
        _senderMock
            .Setup(s => s.SendAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new ShipmentCreatedHandler(
            _senderMock.Object,
            Mock.Of<ILogger<ShipmentCreatedHandler>>());
    }

    [Fact]
    public async Task HandleAsync_WithBuyerEmail_ShouldCallSenderOnce()
    {
        var @event = BuildEvent(buyerEmail: "buyer@example.com");

        await _handler.HandleAsync(@event);

        _senderMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithBuyerEmail_ShouldSendShipmentCreatedType()
    {
        var @event = BuildEvent(buyerEmail: "buyer@example.com");

        await _handler.HandleAsync(@event);

        _senderMock.Verify(
            s => s.SendAsync(
                It.Is<NotificationDto>(n => n.NotificationType == "ShipmentCreated"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithBuyerEmail_ShouldPassCorrectDtoFields()
    {
        var @event = BuildEvent(trackingNumber: "STD-001", buyerEmail: "buyer@example.com");

        await _handler.HandleAsync(@event);

        _senderMock.Verify(
            s => s.SendAsync(
                It.Is<NotificationDto>(n =>
                    n.TrackingNumber == "STD-001" &&
                    n.BuyerEmail == "buyer@example.com" &&
                    n.NewStatus == "Created" &&
                    n.PreviousStatus == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithNullBuyerEmail_ShouldNotCallSender()
    {
        var @event = BuildEvent(buyerEmail: null);

        await _handler.HandleAsync(@event);

        _senderMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithWhitespaceBuyerEmail_ShouldNotCallSender()
    {
        var @event = BuildEvent(buyerEmail: "   ");

        await _handler.HandleAsync(@event);

        _senderMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyBuyerEmail_ShouldNotCallSender()
    {
        var @event = BuildEvent(buyerEmail: string.Empty);

        await _handler.HandleAsync(@event);

        _senderMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ShipmentCreatedEvent BuildEvent(
        string trackingNumber = "STD-001",
        string? buyerEmail = "buyer@example.com") =>
        new(
            ShipmentId: Guid.NewGuid(),
            TrackingNumber: trackingNumber,
            CarrierType: "Steadfast",
            UserId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            BuyerEmail: buyerEmail,
            CreatedAt: DateTime.UtcNow);
}
