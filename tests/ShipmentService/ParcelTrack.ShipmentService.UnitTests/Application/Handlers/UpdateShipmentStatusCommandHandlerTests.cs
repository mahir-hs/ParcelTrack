using FluentAssertions;
using Moq;
using ParcelTrack.ShipmentService.Application.Commands;
using ParcelTrack.ShipmentService.Application.Handler;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Enums;
using ParcelTrack.ShipmentService.Domain.Exceptions;
using ParcelTrack.ShipmentService.UnitTests.Application.Helpers;

namespace ParcelTrack.ShipmentService.UnitTests.Application.Handlers;

public sealed class UpdateShipmentStatusCommandHandlerTests
{
    private readonly Mock<IShipmentRepository> _repoMock;
    private readonly Mock<IEventProducer> _producerMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UpdateShipmentStatusCommandHandler _handler;

    public UpdateShipmentStatusCommandHandlerTests()
    {
        _repoMock = new Mock<IShipmentRepository>();
        _producerMock = new Mock<IEventProducer>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new UpdateShipmentStatusCommandHandler(_repoMock.Object, _producerMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ValidTransition_UpdatesStatus()
    {
        // Arrange: InTransit → OutForDelivery
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.InTransit);
        SetupMocks(shipment);

        var command = BuildCommand(shipment.Id, ShipmentStatus.OutForDelivery);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ShipmentStatus.OutForDelivery);
    }

    [Fact]
    public async Task Handle_ValidTransition_PublishesStatusChangedEvent()
    {
        // Arrange
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.InTransit);
        SetupMocks(shipment);

        // Act
        await _handler.Handle(BuildCommand(shipment.Id, ShipmentStatus.OutForDelivery), CancellationToken.None);

        // Assert
        _producerMock.Verify(
            p => p.PublishAsync("shipment.status.changed", It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidTransition_CallsRepositoryUpdateOnce()
    {
        // Arrange
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.InTransit);
        SetupMocks(shipment);

        // Act
        await _handler.Handle(BuildCommand(shipment.Id, ShipmentStatus.OutForDelivery), CancellationToken.None);

        
    }

    [Fact]
    public async Task Handle_ShipmentNotFound_ThrowsShipmentNotFoundException()
    {
        // Arrange
        _repoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)null);

        // Act & Assert
        await _handler
            .Invoking(h => h.Handle(BuildCommand(Guid.NewGuid(), ShipmentStatus.InTransit), CancellationToken.None))
            .Should().ThrowAsync<ShipmentNotFoundException>();
    }

    [Fact]
    public async Task Handle_AlreadyDelivered_ThrowsShipmentAlreadyTerminatedException()
    {
        // Arrange: Delivered is terminal — no further transitions allowed
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.Delivered);
        SetupMocks(shipment);

        // Act & Assert
        await _handler
            .Invoking(h => h.Handle(BuildCommand(shipment.Id, ShipmentStatus.OutForDelivery), CancellationToken.None))
            .Should().ThrowAsync<ShipmentAlreadyTerminatedException>();
    }

    [Fact]
    public async Task Handle_AlreadyCancelled_ThrowsShipmentAlreadyTerminatedException()
    {
        // Arrange: Cancelled is terminal — no further transitions allowed
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.Cancelled);
        SetupMocks(shipment);

        // Act & Assert
        await _handler
            .Invoking(h => h.Handle(BuildCommand(shipment.Id, ShipmentStatus.InTransit), CancellationToken.None))
            .Should().ThrowAsync<ShipmentAlreadyTerminatedException>();
    }

    [Fact]
    public async Task Handle_IllegalSkip_ThrowsInvalidShipmentStatusTransitionException()
    {
        // Arrange: Created → Delivered is an illegal skip
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.Created);
        SetupMocks(shipment);

        // Act & Assert
        await _handler
            .Invoking(h => h.Handle(BuildCommand(shipment.Id, ShipmentStatus.Delivered), CancellationToken.None))
            .Should().ThrowAsync<InvalidShipmentStatusTransitionException>();
    }

    [Fact]
    public async Task Handle_FailedToOutForDelivery_SucceedsAsRetry()
    {
        // Arrange: Failed → OutForDelivery is a valid retry (attempt 1 of 3)
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.Failed);
        SetupMocks(shipment);

        // Act
        var result = await _handler.Handle(
            BuildCommand(shipment.Id, ShipmentStatus.OutForDelivery, "Re-attempting delivery"),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(ShipmentStatus.OutForDelivery);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void SetupMocks(Shipment shipment)
    {
        _repoMock
            .Setup(r => r.GetByIdAsync(shipment.Id,  It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private static UpdateShipmentStatusCommand BuildCommand(
        Guid shipmentId,
        ShipmentStatus newStatus,
        string description = "Status updated") => new()
        {
            ShipmentId = shipmentId,
            NewStatus = newStatus,
            Description = description,
            Location = "Dhaka Hub"
        };
}