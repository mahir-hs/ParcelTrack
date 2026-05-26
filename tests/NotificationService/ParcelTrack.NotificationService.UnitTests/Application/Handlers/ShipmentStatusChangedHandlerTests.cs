using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParcelTrack.NotificationService.Application.DTOs;
using ParcelTrack.NotificationService.Application.Handlers;
using ParcelTrack.NotificationService.Application.Interfaces;
using ParcelTrack.Shared.Contracts.Events;

namespace ParcelTrack.NotificationService.UnitTests.Application.Handlers;

public sealed class ShipmentStatusChangedHandlerTests
{
    private readonly Mock<INotificationSender> _senderMock;
    private readonly ShipmentStatusChangedHandler _handler;

    public ShipmentStatusChangedHandlerTests()
    {
        _senderMock = new Mock<INotificationSender>();
        _senderMock
            .Setup(s => s.SendAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new ShipmentStatusChangedHandler(
            _senderMock.Object,
            Mock.Of<ILogger<ShipmentStatusChangedHandler>>());
    }

    // ── Notifiable statuses ───────────────────────────────────────────────────

    [Theory]
    [InlineData("InTransit")]
    [InlineData("OutForDelivery")]
    [InlineData("Delivered")]
    [InlineData("Failed")]
    [InlineData("Cancelled")]
    public async Task HandleAsync_WithNotifiableStatus_ShouldCallSender(string newStatus)
    {
        var @event = BuildEvent(buyerEmail: "buyer@example.com", newStatus: newStatus);

        await _handler.HandleAsync(@event);

        _senderMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Created")]
    [InlineData("Pending")]
    [InlineData("Unknown")]
    public async Task HandleAsync_WithNonNotifiableStatus_ShouldNotCallSender(string newStatus)
    {
        var @event = BuildEvent(buyerEmail: "buyer@example.com", newStatus: newStatus);

        await _handler.HandleAsync(@event);

        _senderMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Buyer email gate ──────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WithNullBuyerEmail_ShouldNotCallSender()
    {
        var @event = BuildEvent(buyerEmail: null, newStatus: "Delivered");

        await _handler.HandleAsync(@event);

        _senderMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithWhitespaceBuyerEmail_ShouldNotCallSender()
    {
        var @event = BuildEvent(buyerEmail: "   ", newStatus: "Delivered");

        await _handler.HandleAsync(@event);

        _senderMock.Verify(
            s => s.SendAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── DTO contents ──────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ShouldPassCorrectDtoFields()
    {
        var @event = BuildEvent(
            trackingNumber: "STD-001",
            buyerEmail: "buyer@example.com",
            previousStatus: "InTransit",
            newStatus: "Delivered");

        await _handler.HandleAsync(@event);

        _senderMock.Verify(
            s => s.SendAsync(
                It.Is<NotificationDto>(n =>
                    n.TrackingNumber == "STD-001" &&
                    n.BuyerEmail == "buyer@example.com" &&
                    n.NotificationType == "StatusChanged" &&
                    n.PreviousStatus == "InTransit" &&
                    n.NewStatus == "Delivered"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ShipmentStatusChangedEvent BuildEvent(
        string trackingNumber = "STD-001",
        string? buyerEmail = "buyer@example.com",
        string previousStatus = "Created",
        string newStatus = "InTransit") =>
        new(
            ShipmentId: Guid.NewGuid(),
            TrackingNumber: trackingNumber,
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            BuyerEmail: buyerEmail,
            PreviousStatus: previousStatus,
            NewStatus: newStatus,
            Location: "Dhaka",
            Description: "Status updated",
            OccurredAt: DateTime.UtcNow);
}
