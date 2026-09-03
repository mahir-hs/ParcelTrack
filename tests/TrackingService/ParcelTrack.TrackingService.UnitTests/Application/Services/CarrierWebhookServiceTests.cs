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

namespace ParcelTrack.TrackingService.UnitTests.Application.Services;

public sealed class CarrierWebhookServiceTests
{
    private readonly Mock<ITrackedShipmentRepository> _repository = new();
    private readonly Mock<ICarrierEventPublisher> _publisher = new();
    private readonly Mock<ICarrierAdapter> _adapter = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero));

    public CarrierWebhookServiceTests()
    {
        _adapter.SetupGet(a => a.Carrier).Returns(CarrierType.Pathao);
    }

    private CarrierWebhookService CreateService()
    {
        var applier = new CarrierObservationApplier(
            _publisher.Object,
            Mock.Of<ILogger<CarrierObservationApplier>>());

        return new CarrierWebhookService(
            [_adapter.Object],
            _repository.Object,
            applier,
            _clock,
            Mock.Of<ILogger<CarrierWebhookService>>());
    }

    private static TrackedShipment Shipment() =>
        TrackedShipment.Create(
            Guid.NewGuid(), "DA001", CarrierType.Pathao,
            Guid.NewGuid(), Guid.NewGuid(), "buyer@example.com",
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

    private static CarrierTrackingResult Observation(CarrierStatus status = CarrierStatus.InTransit) => new()
    {
        TrackingNumber = "DA001",
        Status = status,
        RawStatus = status.ToString(),
        Description = status.ToString(),
        OccurredAt = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc),
        Carrier = CarrierType.Pathao
    };

    [Fact]
    public async Task IngestAsync_ShouldApplyAndPublishNewStatus()
    {
        _adapter.Setup(a => a.ParseWebhookPayload(It.IsAny<string>())).Returns(Observation());
        _repository.Setup(r => r.GetByTrackingNumberAsync("DA001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Shipment());

        var outcome = await CreateService().IngestAsync(CarrierType.Pathao, "{}");

        outcome.Should().Be(WebhookIngestOutcome.Applied);
        _publisher.Verify(p => p.PublishObservationAsync(
            It.IsAny<CarrierStatusObservedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestAsync_ShouldReportNoChangeWhenStatusAlreadyKnown()
    {
        // The poller may have seen it first — whichever route wins, the other stays quiet.
        var shipment = Shipment();
        shipment.SyncStatus(CarrierStatus.InTransit, DateTime.UtcNow);

        _adapter.Setup(a => a.ParseWebhookPayload(It.IsAny<string>())).Returns(Observation());
        _repository.Setup(r => r.GetByTrackingNumberAsync("DA001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        var outcome = await CreateService().IngestAsync(CarrierType.Pathao, "{}");

        outcome.Should().Be(WebhookIngestOutcome.NoChange);
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IngestAsync_ShouldReportNotTrackedForUnknownConsignment()
    {
        // Couriers push for every parcel on the merchant account, not just ours.
        _adapter.Setup(a => a.ParseWebhookPayload(It.IsAny<string>())).Returns(Observation());
        _repository.Setup(r => r.GetByTrackingNumberAsync("DA001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackedShipment?)null);

        var outcome = await CreateService().IngestAsync(CarrierType.Pathao, "{}");

        outcome.Should().Be(WebhookIngestOutcome.NotTracked);
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IngestAsync_ShouldReportUnparseableForBadPayload()
    {
        _adapter.Setup(a => a.ParseWebhookPayload(It.IsAny<string>())).Returns((CarrierTrackingResult?)null);

        var outcome = await CreateService().IngestAsync(CarrierType.Pathao, "not json");

        outcome.Should().Be(WebhookIngestOutcome.Unparseable);
    }

    [Fact]
    public async Task IngestAsync_ShouldReportUnknownCarrierWhenNoAdapterRegistered()
    {
        var outcome = await CreateService().IngestAsync(CarrierType.Redx, "{}");

        outcome.Should().Be(WebhookIngestOutcome.UnknownCarrier);
    }

    [Fact]
    public async Task IngestAsync_ShouldSaveAfterApplying()
    {
        _adapter.Setup(a => a.ParseWebhookPayload(It.IsAny<string>())).Returns(Observation());
        _repository.Setup(r => r.GetByTrackingNumberAsync("DA001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Shipment());

        await CreateService().IngestAsync(CarrierType.Pathao, "{}");

        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestAsync_ShouldDeactivateOnTerminalStatus()
    {
        var shipment = Shipment();
        _adapter.Setup(a => a.ParseWebhookPayload(It.IsAny<string>()))
            .Returns(Observation(CarrierStatus.Delivered));
        _repository.Setup(r => r.GetByTrackingNumberAsync("DA001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

        await CreateService().IngestAsync(CarrierType.Pathao, "{}");

        shipment.IsActive.Should().BeFalse();
    }
}
