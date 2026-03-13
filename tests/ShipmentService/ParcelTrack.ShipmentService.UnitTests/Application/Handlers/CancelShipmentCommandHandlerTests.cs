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

public sealed class CancelShipmentCommandHandlerTests
{
    private readonly Mock<IShipmentRepository> _repoMock;
    private readonly Mock<IEventProducer> _producerMock;
    private readonly CancelShipmentCommandHandler _handler;

    public CancelShipmentCommandHandlerTests()
    {
        _repoMock = new Mock<IShipmentRepository>();
        _producerMock = new Mock<IEventProducer>();
        _handler = new CancelShipmentCommandHandler(_repoMock.Object, _producerMock.Object);
    }

    [Fact]
    public async Task Handle_CreatedShipment_CancelsSuccessfully()
    {
        // Arrange
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.Created);
        SetupMocks(shipment);

        // Act
        var result = await _handler.Handle(BuildCommand(shipment.Id), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ShipmentStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_InTransitShipment_CancelsSuccessfully()
    {
        // Arrange: cancellation from InTransit is also a valid transition
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.InTransit);
        SetupMocks(shipment);

        // Act
        var result = await _handler.Handle(BuildCommand(shipment.Id), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ShipmentStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_DeliveredShipment_ThrowsShipmentAlreadyTerminatedException()
    {
        // Arrange: cannot cancel a delivered shipment — it is terminal
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.Delivered);
        SetupMocks(shipment);

        // Act & Assert
        await _handler
            .Invoking(h => h.Handle(BuildCommand(shipment.Id), CancellationToken.None))
            .Should().ThrowAsync<ShipmentAlreadyTerminatedException>();
    }

    [Fact]
    public async Task Handle_AlreadyCancelledShipment_ThrowsShipmentAlreadyTerminatedException()
    {
        // Arrange: cannot cancel an already cancelled shipment
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.Cancelled);
        SetupMocks(shipment);

        // Act & Assert
        await _handler
            .Invoking(h => h.Handle(BuildCommand(shipment.Id), CancellationToken.None))
            .Should().ThrowAsync<ShipmentAlreadyTerminatedException>();
    }

    [Fact]
    public async Task Handle_ShipmentNotFound_ThrowsShipmentNotFoundException()
    {
        // Arrange
        _repoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)null);

        // Act & Assert
        await _handler
            .Invoking(h => h.Handle(BuildCommand(Guid.NewGuid()), CancellationToken.None))
            .Should().ThrowAsync<ShipmentNotFoundException>();
    }

    [Fact]
    public async Task Handle_ValidCancellation_PublishesStatusChangedEvent()
    {
        // Arrange
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.Created);
        SetupMocks(shipment);

        // Act
        await _handler.Handle(BuildCommand(shipment.Id), CancellationToken.None);

        // Assert
        _producerMock.Verify(
            p => p.PublishAsync("shipment.status.changed", It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCancellation_CallsRepositoryUpdateOnce()
    {
        // Arrange
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.Created);
        SetupMocks(shipment);

        // Act
        await _handler.Handle(BuildCommand(shipment.Id), CancellationToken.None);

        // Assert
        _repoMock.Verify(
            r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void SetupMocks(Shipment shipment)
    {
        _repoMock
            .Setup(r => r.GetByIdAsync(shipment.Id, shipment.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)shipment);

        _repoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static CancelShipmentCommand BuildCommand(Guid shipmentId) => new()
    {
        ShipmentId = shipmentId,
        Reason = "Cancelled by seller"
    };
}