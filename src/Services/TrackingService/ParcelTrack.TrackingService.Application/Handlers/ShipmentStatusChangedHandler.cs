using Microsoft.Extensions.Logging;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.Application.Handlers;

public sealed class ShipmentStatusChangedHandler(
    ITrackingRepository repository,
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

        logger.LogInformation(
            "Tracking record added for {TrackingNumber}: {Previous} → {New}",
            @event.TrackingNumber, @event.PreviousStatus, @event.NewStatus);
    }
}
