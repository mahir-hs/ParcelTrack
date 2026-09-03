using Microsoft.Extensions.Logging;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;
using ParcelTrack.TrackingService.Domain.Enums;

namespace ParcelTrack.TrackingService.Application.Handlers;

public sealed class ShipmentCreatedHandler(
    ITrackingRepository repository,
    ITrackedShipmentRepository trackedShipments,
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

        await RegisterForPollingAsync(@event, cancellationToken);

        logger.LogInformation(
            "Tracking record created for shipment {ShipmentId} ({TrackingNumber})",
            @event.ShipmentId, @event.TrackingNumber);
    }

    /// <summary>
    /// Adds the shipment to the polling registry so the courier starts being asked about it.
    ///
    /// Idempotent: Kafka delivers at least once, so a redelivered ShipmentCreated must not
    /// create a second parcel to poll. The unique index on tracking_number is the backstop;
    /// this check keeps the common case from reaching it.
    /// </summary>
    private async Task RegisterForPollingAsync(
        ShipmentCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        var existing = await trackedShipments.GetByTrackingNumberAsync(@event.TrackingNumber, cancellationToken);
        if (existing is not null)
        {
            logger.LogDebug("{TrackingNumber} is already registered for polling", @event.TrackingNumber);
            return;
        }

        if (!Enum.TryParse<CarrierType>(@event.CarrierType, ignoreCase: true, out var carrier))
        {
            // ShipmentService accepted a carrier this service has no adapter for. Log the
            // parcel rather than dropping it silently — but there is nothing to poll.
            logger.LogWarning(
                "Not polling {TrackingNumber}: unrecognised carrier {CarrierType}",
                @event.TrackingNumber, @event.CarrierType);
            return;
        }

        await trackedShipments.AddAsync(
            TrackedShipment.Create(
                @event.ShipmentId,
                @event.TrackingNumber,
                carrier,
                @event.TenantId,
                @event.UserId,
                @event.BuyerEmail,
                @event.CreatedAt),
            cancellationToken);

        await trackedShipments.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Registered {TrackingNumber} for {Carrier} polling",
            @event.TrackingNumber, carrier);
    }
}
