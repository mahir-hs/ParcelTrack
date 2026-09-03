using Microsoft.Extensions.Logging;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;
using ParcelTrack.TrackingService.Domain.Enums;

namespace ParcelTrack.TrackingService.Application.Handlers;

public sealed class ShipmentStatusChangedHandler(
    ITrackingRepository repository,
    ITrackedShipmentRepository trackedShipments,
    ILogger<ShipmentStatusChangedHandler> logger)
{
    public async Task HandleAsync(ShipmentStatusChangedEvent @event, CancellationToken cancellationToken = default)
    {
        var record = TrackingRecord.Create(
            @event.ShipmentId,
            @event.TrackingNumber,
            @event.NewStatus,
            @event.Description,
            carrierType: string.Empty,
            @event.TenantId,
            @event.OccurredAt,
            @event.Location);

        await repository.AddAsync(record, cancellationToken);

        await SyncPollingRegistryAsync(@event, cancellationToken);

        logger.LogInformation(
            "Tracking record added for {TrackingNumber}: {Previous} → {New}",
            @event.TrackingNumber, @event.PreviousStatus, @event.NewStatus);
    }

    /// <summary>
    /// Keeps the polling registry aligned with status changes that came from elsewhere —
    /// a manual API call, or this service's own poller round-tripping through Kafka.
    ///
    /// Two things matter here. A parcel cancelled through the API must stop being polled, and
    /// the poller's own published event must not be mistaken for new information when it comes
    /// back: SyncStatus records the status without treating it as a fresh observation, so the
    /// next poll compares against the truth and stays quiet.
    /// </summary>
    private async Task SyncPollingRegistryAsync(
        ShipmentStatusChangedEvent @event,
        CancellationToken cancellationToken)
    {
        var tracked = await trackedShipments.GetByTrackingNumberAsync(@event.TrackingNumber, cancellationToken);
        if (tracked is null)
            return;

        if (Enum.TryParse<CarrierStatus>(@event.NewStatus, ignoreCase: true, out var status))
        {
            tracked.SyncStatus(status, @event.OccurredAt);
        }
        else
        {
            logger.LogWarning(
                "Cannot sync polling registry for {TrackingNumber}: unrecognised status {Status}",
                @event.TrackingNumber, @event.NewStatus);
        }

        await trackedShipments.SaveChangesAsync(cancellationToken);
    }
}
