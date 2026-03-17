using FluentAssertions;
using Moq;
using ParcelTrack.ShipmentService.Application.Handler;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Application.Queries;
using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Enums;
using ParcelTrack.ShipmentService.Domain.Exceptions;
using ParcelTrack.ShipmentService.UnitTests.Application.Helpers;

namespace ParcelTrack.ShipmentService.UnitTests.Application.Handlers;

public sealed class GetShipmentByIdQueryHandlerTests
{
    private readonly Mock<IShipmentRepository> _repoMock;
    private readonly GetShipmentByIdQueryHandler _handler;

    public GetShipmentByIdQueryHandlerTests()
    {
        _repoMock = new Mock<IShipmentRepository>();
        _handler = new GetShipmentByIdQueryHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_ExistingShipment_ReturnsMappedDto()
    {
        // Arrange
        var shipment = ShipmentFactory.Create(trackingNumber: "STD-GET-001");

        _repoMock
            .Setup(r => r.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        // Act
        var result = await _handler.Handle(new GetShipmentByIdQuery { ShipmentId = shipment.Id, TenantId = shipment.TenantId }, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(shipment.Id);
        result.TrackingNumber.Should().Be("STD-GET-001");
        result.Status.Should().Be(ShipmentStatus.Created);
    }

    [Fact]
    public async Task Handle_ExistingShipment_ReturnsCorrectTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var shipment = ShipmentFactory.Create(tenantId: tenantId);

        _repoMock
            .Setup(r => r.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        // Act
        var result = await _handler.Handle(new GetShipmentByIdQuery { ShipmentId = shipment.Id, TenantId = shipment.TenantId }, CancellationToken.None);

        // Assert
        result.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task Handle_ShipmentWithEvents_ReturnsCorrectEventCount()
    {
        // Arrange: 2 status changes = 2 events in history
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.OutForDelivery);

        _repoMock
            .Setup(r => r.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        // Act
        var result = await _handler.Handle(new GetShipmentByIdQuery { ShipmentId = shipment.Id, TenantId = shipment.TenantId }, CancellationToken.None);

        // Assert
        result.Events.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_ShipmentWithEvents_EventsOrderedByOccurredAt()
    {
        // Arrange
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.OutForDelivery);

        _repoMock
            .Setup(r => r.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        // Act
        var result = await _handler.Handle(new GetShipmentByIdQuery { ShipmentId = shipment.Id, TenantId = shipment.TenantId }, CancellationToken.None);

        // Assert
        result.Events.Should().BeInAscendingOrder(e => e.OccurredAt);
    }

    [Fact]
    public async Task Handle_NonExistentShipment_ThrowsShipmentNotFoundException()
    {
        // Arrange
        _repoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)null);

        // Act & Assert
        await _handler
            .Invoking(h => h.Handle(new GetShipmentByIdQuery { ShipmentId = Guid.NewGuid(), TenantId = Guid.NewGuid() }, CancellationToken.None))
            .Should().ThrowAsync<ShipmentNotFoundException>();
    }
}