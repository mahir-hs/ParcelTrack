using FluentAssertions;
using Moq;
using ParcelTrack.ShipmentService.Application.Commands;
using ParcelTrack.ShipmentService.Application.Handler;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.UnitTests.Application.Handlers;

public sealed class CreateShipmentCommandHandlerTests
{
    private readonly Mock<IShipmentRepository> _repoMock;
    private readonly Mock<IEventProducer> _producerMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateShipmentCommandHandler _handler;

    public CreateShipmentCommandHandlerTests()
    {
        _repoMock = new Mock<IShipmentRepository>();
        _producerMock = new Mock<IEventProducer>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new CreateShipmentCommandHandler(_repoMock.Object, _producerMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsShipmentDto()
    {
        // Arrange
        SetupMocks();
        var command = BuildCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TrackingNumber.Should().Be(command.TrackingNumber);
        result.CarrierType.Should().Be(command.CarrierType);
        result.Status.Should().Be(ShipmentStatus.Created);
    }

    [Fact]
    public async Task Handle_ValidCommand_SetsCorrectTenantAndUser()
    {
        // Arrange
        SetupMocks();
        var command = BuildCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.UserId.Should().Be(command.UserId);
        result.TenantId.Should().Be(command.TenantId);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnsEvents()
    {
        SetupMocks();

        // Act
        var result = await _handler.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.Events.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task Handle_ValidCommand_CallsRepositoryAddOnce()
    {
        // Arrange
        SetupMocks();

        // Act
        await _handler.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        _repoMock.Verify(
            r => r.AddAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_PublishesShipmentCreatedEvent()
    {
        // Arrange
        SetupMocks();

        // Act
        await _handler.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        _producerMock.Verify(
            p => p.PublishAsync("shipment.created", It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void SetupMocks()
    {
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private static CreateShipmentCommand BuildCommand() => new()
    {
        TrackingNumber = "STD-001",
        CarrierType = CarrierType.Steadfast,
        BuyerEmail = "buyer@example.com",
        UserId = Guid.NewGuid(),
        TenantId = Guid.NewGuid()
    };
}