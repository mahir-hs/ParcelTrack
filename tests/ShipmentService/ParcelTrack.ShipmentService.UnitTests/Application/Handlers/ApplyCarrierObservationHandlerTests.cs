using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.ShipmentService.Application.Handler;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Enums;
using ParcelTrack.ShipmentService.UnitTests.Application.Helpers;

namespace ParcelTrack.ShipmentService.UnitTests.Application.Handlers;

/// <summary>
/// The courier's observations are applied through the real UpdateShipmentStatusCommandHandler,
/// not a mock of it — the whole point is that a courier gets no authority the domain would not
/// grant an API caller, and only the real handler proves that.
/// </summary>
public sealed class ApplyCarrierObservationHandlerTests
{
    private readonly Mock<IShipmentRepository> _repoMock = new();
    private readonly Mock<IEventProducer> _producerMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private ApplyCarrierObservationHandler CreateHandler() =>
        new(
            new UpdateShipmentStatusCommandHandler(
                _repoMock.Object, _producerMock.Object, _unitOfWorkMock.Object),
            Mock.Of<ILogger<ApplyCarrierObservationHandler>>());

    private void SetupShipment(Shipment shipment) =>
        _repoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

    private static CarrierStatusObservedEvent Observation(
        Guid shipmentId,
        string observedStatus,
        Guid? tenantId = null) =>
        new(
            shipmentId,
            "DA240101ABCDE",
            tenantId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            Carrier: "Pathao",
            ObservedStatus: observedStatus,
            RawStatus: observedStatus,
            Description: observedStatus,
            Location: "Gulshan Hub",
            OccurredAt: new DateTime(2026, 3, 9, 9, 0, 0, DateTimeKind.Utc));

    // ── Applying valid observations ─────────────────────────────

    [Fact]
    public async Task HandleAsync_ShouldApplyValidTransition()
    {
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.InTransit);
        SetupShipment(shipment);

        var result = await CreateHandler().HandleAsync(
            Observation(shipment.Id, nameof(ShipmentStatus.OutForDelivery)));

        result.Should().Be(CarrierObservationResult.Applied);
        shipment.Status.Should().Be(ShipmentStatus.OutForDelivery);
    }

    [Fact]
    public async Task HandleAsync_ShouldPublishAuthoritativeEventOnApply()
    {
        // The observation is not forwarded — ShipmentService republishes its own decision,
        // which is what Notification and Webhook services consume.
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.InTransit);
        SetupShipment(shipment);

        await CreateHandler().HandleAsync(
            Observation(shipment.Id, nameof(ShipmentStatus.OutForDelivery)));

        _producerMock.Verify(p => p.PublishAsync(
            It.IsAny<string>(),
            It.IsAny<ShipmentStatusChangedEvent>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldSaveOnApply()
    {
        // OutForDelivery → Delivered; InTransit → Delivered is not a legal transition.
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.OutForDelivery);
        SetupShipment(shipment);

        await CreateHandler().HandleAsync(
            Observation(shipment.Id, nameof(ShipmentStatus.Delivered)));

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("outfordelivery")]
    [InlineData("OUTFORDELIVERY")]
    [InlineData("OutForDelivery")]
    public async Task HandleAsync_ShouldParseStatusCaseInsensitively(string observedStatus)
    {
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.InTransit);
        SetupShipment(shipment);

        var result = await CreateHandler().HandleAsync(Observation(shipment.Id, observedStatus));

        result.Should().Be(CarrierObservationResult.Applied);
    }

    // ── The domain still rules ──────────────────────────────────

    [Fact]
    public async Task HandleAsync_ShouldRejectImpossibleTransition()
    {
        // A courier claiming a brand-new parcel is delivered does not make it so.
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.Created);
        SetupShipment(shipment);

        var result = await CreateHandler().HandleAsync(
            Observation(shipment.Id, nameof(ShipmentStatus.Delivered)));

        result.Should().Be(CarrierObservationResult.Rejected);
        shipment.Status.Should().Be(ShipmentStatus.Created);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectObservationForTerminatedShipment()
    {
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.Delivered);
        SetupShipment(shipment);

        var result = await CreateHandler().HandleAsync(
            Observation(shipment.Id, nameof(ShipmentStatus.InTransit)));

        result.Should().Be(CarrierObservationResult.Rejected);
        shipment.Status.Should().Be(ShipmentStatus.Delivered);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectRepeatedObservationOfSameStatus()
    {
        // Polling re-reports the same status constantly; the second one is a no-op transition.
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.InTransit);
        SetupShipment(shipment);

        var result = await CreateHandler().HandleAsync(
            Observation(shipment.Id, nameof(ShipmentStatus.InTransit)));

        result.Should().Be(CarrierObservationResult.Rejected);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotPublishWhenRejected()
    {
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.Created);
        SetupShipment(shipment);

        await CreateHandler().HandleAsync(
            Observation(shipment.Id, nameof(ShipmentStatus.Delivered)));

        _producerMock.Verify(p => p.PublishAsync(
            It.IsAny<string>(),
            It.IsAny<ShipmentStatusChangedEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectWhenShipmentNotFound()
    {
        // Wrong tenant, or deleted. Never retryable — the consumer must not die on it.
        _repoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)null);

        var result = await CreateHandler().HandleAsync(
            Observation(Guid.NewGuid(), nameof(ShipmentStatus.InTransit)));

        result.Should().Be(CarrierObservationResult.Rejected);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectWhenDeliveryAttemptsExceeded()
    {
        // The courier keeps trying past the agreed cap of 3.
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.InTransit);
        SetupShipment(shipment);

        for (var i = 0; i < 3; i++)
        {
            shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "attempt", null);
            shipment.UpdateStatus(ShipmentStatus.Failed, "missed", null);
        }

        var result = await CreateHandler().HandleAsync(
            Observation(shipment.Id, nameof(ShipmentStatus.OutForDelivery)));

        result.Should().Be(CarrierObservationResult.Rejected);
    }

    // ── Statuses with no shipment equivalent ────────────────────

    [Theory]
    [InlineData("Returned")]
    [InlineData("Unknown")]
    public async Task HandleAsync_ShouldIgnoreStatusesWithNoShipmentEquivalent(string observedStatus)
    {
        var shipment = ShipmentFactory.WithStatus(ShipmentStatus.InTransit);
        SetupShipment(shipment);

        var result = await CreateHandler().HandleAsync(Observation(shipment.Id, observedStatus));

        result.Should().Be(CarrierObservationResult.NotApplicable);
        shipment.Status.Should().Be(ShipmentStatus.InTransit);
    }

    [Theory]
    [InlineData("Teleported")]
    [InlineData("")]
    [InlineData("42")]
    public async Task HandleAsync_ShouldIgnoreUnrecognisedStatus(string observedStatus)
    {
        var result = await CreateHandler().HandleAsync(
            Observation(Guid.NewGuid(), observedStatus));

        result.Should().Be(CarrierObservationResult.NotApplicable);
        _repoMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
