using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ParcelTrack.NotificationService.Application;
using ParcelTrack.NotificationService.Application.Domain;
using ParcelTrack.NotificationService.Application.Handlers;
using ParcelTrack.NotificationService.Application.Interfaces;
using ParcelTrack.Shared.Contracts;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.Shared.Messaging;
using Xunit;

namespace ParcelTrack.NotificationService.UnitTests.Application.Handlers;

public sealed class ShipmentStatusChangedEventHandlerTests
{
    private readonly Mock<INotificationRepository> _repositoryMock;
    private readonly Mock<INotificationSender> _senderMock;
    private readonly Mock<IKafkaProducer> _kafkaMock;
    private readonly Mock<ILogger<ShipmentStatusChangedEventHandler>> _loggerMock;
    private readonly Mock<IOptions<NotificationOptions>> _optionsMock;
    private readonly ShipmentStatusChangedEventHandler _handler;

    public ShipmentStatusChangedEventHandlerTests()
    {
        _repositoryMock = new Mock<INotificationRepository>();
        _senderMock = new Mock<INotificationSender>();
        _kafkaMock = new Mock<IKafkaProducer>();
        _loggerMock = new Mock<ILogger<ShipmentStatusChangedEventHandler>>();
        _optionsMock = new Mock<IOptions<NotificationOptions>>();
        _optionsMock.Setup(o => o.Value).Returns(new NotificationOptions());

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repositoryMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _senderMock
            .Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _kafkaMock
            .Setup(k => k.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new ShipmentStatusChangedEventHandler(
            _repositoryMock.Object, _senderMock.Object, _kafkaMock.Object, _optionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_StatusNotInNotifySet_ReturnsEarlyWithoutSendingOrPersisting()
    {
        // Arrange: "InTransit" is not in the notify set
        var e = BuildEvent(newStatus: "InTransit");

        // Act
        await _handler.Handle(e, CancellationToken.None);

        // Assert
        e.NewStatus.Should().Be("InTransit");
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _senderMock.Verify(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _kafkaMock.Verify(k => k.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NotifyStatus_DeliversAndPersistsWithoutKafkaPublish()
    {
        // Arrange: "Delivered" is in the notify set
        var e = BuildEvent(newStatus: "Delivered");

        // Act
        await _handler.Handle(e, CancellationToken.None);

        // Assert
        e.NewStatus.Should().Be("Delivered");
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        _senderMock.Verify(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        // SaveChangesAsync is called twice: once after Add, once after MarkSent.
        _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        // A successful delivery must not publish a failure event.
        _kafkaMock.Verify(k => k.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SendThrows_RecordsFailureButDoesNotDeadLetterOnSingleAttempt()
    {
        // Arrange: "Delivered" is in the notify set, but delivery always fails
        var e = BuildEvent(newStatus: "Delivered");
        Notification? captured = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => captured = n)
            .Returns(Task.CompletedTask);
        _senderMock
            .Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mail provider down"));

        // Act
        await _handler.Handle(e, CancellationToken.None);

        // Assert: the failure path ran — RecordFailure incremented attempts and stored the error.
        e.NewStatus.Should().Be("Delivered");
        captured.Should().NotBeNull();
        captured!.Status.Should().Be("Pending"); // not yet Failed on a single attempt
        captured.Attempts.Should().Be(1);
        captured.Error.Should().Be("mail provider down");
        captured.SentAt.Should().BeNull();

        _senderMock.Verify(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

        // A single failed delivery is attempt #1 (< MaxAttempts), so no dead-letter publish yet.
        // The ShouldDeadLetter → ProduceAsync transition is covered by NotificationTests
        // (3 RecordFailure calls flip Status to "Failed"), matching this handler's guard.
        _kafkaMock.Verify(k => k.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static ShipmentStatusChangedEvent BuildEvent(string newStatus) => new(
        ShipmentId: Guid.NewGuid(),
        TrackingNumber: "TRK-XYZ",
        TenantId: Guid.NewGuid(),
        UserId: Guid.NewGuid(),
        PreviousStatus: "OutForDelivery",
        NewStatus: newStatus,
        Location: "Berlin",
        Description: "status changed",
        OccurredAt: DateTime.UtcNow);
}
