using Microsoft.Extensions.Logging;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.Application.Handlers;

public sealed class ShipmentCreatedHandler(
    ITrackingRepository repository,
    ILogger<ShipmentCreatedHandler> logger)
{
    public async Task HandleAsync(ShipmentCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        var record = TrackingRecord.Create(
            @event.ShipmentId,
            @event.TrackingNumber,
            status: "Created",
            description: "Shipment registered in ParcelTrack.",
            @event.CarrierType,
            @event.TenantId,
            @event.CreatedAt);

        await repository.AddAsync(record, cancellationToken);

        logger.LogInformation(
            "Tracking record created for shipment {ShipmentId} ({TrackingNumber})",
            @event.ShipmentId, @event.TrackingNumber);
    }
}
