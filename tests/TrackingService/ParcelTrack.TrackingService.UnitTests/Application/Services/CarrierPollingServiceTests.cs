using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.DTOs;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Application.Services;
using ParcelTrack.TrackingService.Domain.Entities;
using ParcelTrack.TrackingService.Domain.Enums;
using ParcelTrack.TrackingService.Domain.Exceptions;

namespace ParcelTrack.TrackingService.UnitTests.Application.Services;

public sealed class CarrierPollingServiceTests
{
    private readonly Mock<ITrackedShipmentRepository> _repository = new();
    private readonly Mock<ICarrierEventPublisher> _publisher = new();
    private readonly Mock<ICarrierAdapter> _adapter = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero));

    public CarrierPollingServiceTests()
    {
        _adapter.SetupGet(a => a.Carrier).Returns(CarrierType.Pathao);
    }

    private CarrierPollingService CreateService()
    {
        var applier = new CarrierObservationApplier(
            _publisher.Object,
            Mock.Of<ILogger<CarrierObservationApplier>>());

        return new CarrierPollingService(
            [_adapter.Object],
            _repository.Object,
            applier,
            _clock,
            Mock.Of<ILogger<CarrierPollingService>>());
    }

    private static TrackedShipment Shipment(string trackingNumber = "DA001") =>
        TrackedShipment.Create(
            Guid.NewGuid(), trackingNumber, CarrierType.Pathao,
            Guid.NewGuid(), Guid.NewGuid(), "buyer@example.com",
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

    private static CarrierTrackingResult Observation(
        CarrierStatus status = CarrierStatus.InTransit,
        string trackingNumber = "DA001") => new()
        {
            TrackingNumber = trackingNumber,
            Status = status,
            RawStatus = status.ToString(),
            Description = status.ToString(),
            OccurredAt = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc),
            Carrier = CarrierType.Pathao
        };

    private void SetupActive(params TrackedShipment[] shipments) =>
        _repository
            .Setup(r => r.GetActiveByCarrierAsync(CarrierType.Pathao, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipments);

    // ── Change detection ────────────────────────────────────────

    [Fact]
    public async Task PollAsync_ShouldPublishWhenStatusChanged()
    {
        SetupActive(Shipment());
        _adapter.Setup(a => a.GetStatusAsync("DA001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Observation(CarrierStatus.InTransit));

        var published = await CreateService().PollAsync(batchSize: 50);

        published.Should().Be(1);
        _publisher.Verify(p => p.PublishStatusChangedAsync(
            It.IsAny<ShipmentStatusChangedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PollAsync_ShouldNotPublishWhenStatusUnchanged()
    {
        // The courier answers with the same status on nearly every cycle.
        var shipment = Shipment();
        shipment.SyncStatus(CarrierStatus.InTransit, DateTime.UtcNow);
        SetupActive(shipment);

        _adapter.Setup(a => a.GetStatusAsync("DA001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Observation(CarrierStatus.InTransit));

        var published = await CreateService().PollAsync(batchSize: 50);

        published.Should().Be(0);
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PollAsync_ShouldCarryShipmentIdentityIntoPublishedEvent()
    {
        var shipment = Shipment();
        SetupActive(shipment);
        _adapter.Setup(a => a.GetStatusAsync("DA001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Observation(CarrierStatus.OutForDelivery));

        ShipmentStatusChangedEvent? captured = null;
        _publisher.Setup(p => p.PublishStatusChangedAsync(
                It.IsAny<ShipmentStatusChangedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ShipmentStatusChangedEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        await CreateService().PollAsync(batchSize: 50);

        // BuyerEmail and TenantId must survive: the notification service needs one, the
        // webhook service the other.
        captured.Should().NotBeNull();
        captured!.ShipmentId.Should().Be(shipment.ShipmentId);
        captured.TenantId.Should().Be(shipment.TenantId);
        captured.BuyerEmail.Should().Be("buyer@example.com");
        captured.PreviousStatus.Should().Be(nameof(CarrierStatus.Created));
        captured.NewStatus.Should().Be(nameof(CarrierStatus.OutForDelivery));
    }

    [Fact]
    public async Task PollAsync_ShouldDeactivateShipmentOnTerminalStatus()
    {
        var shipment = Shipment();
        SetupActive(shipment);
        _adapter.Setup(a => a.GetStatusAsync("DA001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Observation(CarrierStatus.Delivered));

        await CreateService().PollAsync(batchSize: 50);

        shipment.IsActive.Should().BeFalse();
    }

    // ── Resilience within a cycle ───────────────────────────────

    [Fact]
    public async Task PollAsync_ShouldContinueBatchAfterOneCarrierFailure()
    {
        // One dead parcel must not abandon the other 49.
        SetupActive(Shipment("DA001"), Shipment("DA002"), Shipment("DA003"));

        _adapter.Setup(a => a.GetStatusAsync("DA001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Observation(CarrierStatus.InTransit, "DA001"));
        _adapter.Setup(a => a.GetStatusAsync("DA002", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CarrierApiException("Pathao", "boom"));
        _adapter.Setup(a => a.GetStatusAsync("DA003", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Observation(CarrierStatus.Delivered, "DA003"));

        var published = await CreateService().PollAsync(batchSize: 50);

        published.Should().Be(2);
    }

    [Fact]
    public async Task PollAsync_ShouldSurviveUnexpectedException()
    {
        SetupActive(Shipment("DA001"), Shipment("DA002"));

        _adapter.Setup(a => a.GetStatusAsync("DA001", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unexpected"));
        _adapter.Setup(a => a.GetStatusAsync("DA002", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Observation(CarrierStatus.InTransit, "DA002"));

        var published = await CreateService().PollAsync(batchSize: 50);

        published.Should().Be(1);
    }

    [Fact]
    public async Task PollAsync_ShouldHandleConsignmentUnknownToCarrier()
    {
        // Booked with us, not yet handed to the courier.
        SetupActive(Shipment());
        _adapter.Setup(a => a.GetStatusAsync("DA001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CarrierTrackingResult?)null);

        var published = await CreateService().PollAsync(batchSize: 50);

        published.Should().Be(0);
        _publisher.VerifyNoOtherCalls();
    }

    // ── Cycle mechanics ─────────────────────────────────────────

    [Fact]
    public async Task PollAsync_ShouldSaveOnceAfterBatch()
    {
        // LastPolledAt must persist for every parcel touched, changed or not.
        SetupActive(Shipment("DA001"), Shipment("DA002"));
        _adapter.Setup(a => a.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string tn, CancellationToken _) => Observation(CarrierStatus.InTransit, tn));

        await CreateService().PollAsync(batchSize: 50);

        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PollAsync_ShouldDoNothingWhenNoActiveShipments()
    {
        SetupActive();

        var published = await CreateService().PollAsync(batchSize: 50);

        published.Should().Be(0);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _adapter.Verify(a => a.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PollAsync_ShouldRespectBatchSize()
    {
        SetupActive(Shipment());
        _adapter.Setup(a => a.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Observation());

        await CreateService().PollAsync(batchSize: 25);

        _repository.Verify(r => r.GetActiveByCarrierAsync(
            CarrierType.Pathao, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PollAsync_ShouldStopWhenCancelled()
    {
        SetupActive(Shipment("DA001"), Shipment("DA002"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var published = await CreateService().PollAsync(batchSize: 50, cts.Token);

        published.Should().Be(0);
    }
}
