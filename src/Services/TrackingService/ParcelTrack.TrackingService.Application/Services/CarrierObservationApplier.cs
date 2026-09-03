using Microsoft.Extensions.Logging;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.DTOs;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;
using ParcelTrack.TrackingService.Domain.Enums;

namespace ParcelTrack.TrackingService.Application.Services;

/// <summary>
/// Applies one courier observation to a tracked shipment and publishes an event if it changed.
///
/// Shared by both routes an observation can arrive on — the polling worker and the inbound
/// webhook endpoints — so a status pushed by Pathao and the same status discovered by polling
/// produce identical downstream behaviour. Change detection living in exactly one place is
/// also what makes the two routes safe to run simultaneously: whichever sees the change first
/// publishes, and the other finds nothing new to report.
/// </summary>
public sealed class CarrierObservationApplier(
    ICarrierEventPublisher publisher,
    ILogger<CarrierObservationApplier> logger)
{
    /// <summary>
    /// Returns true when the observation was new and an event was published.
    /// Does not save — the caller owns the transaction boundary.
    /// </summary>
    public async Task<bool> ApplyAsync(
        TrackedShipment shipment,
        CarrierTrackingResult observation,
        DateTime observedAt,
        CancellationToken cancellationToken = default)
    {
        var previousStatus = shipment.LastKnownStatus;

        if (!shipment.TryRecordObservedStatus(observation.Status, observedAt))
        {
            logger.LogDebug(
                "No change for {TrackingNumber} — still {Status}",
                shipment.TrackingNumber, previousStatus);
            return false;
        }

        await publisher.PublishStatusChangedAsync(
            BuildEvent(shipment, observation, previousStatus),
            cancellationToken);

        logger.LogInformation(
            "{TrackingNumber}: {Previous} → {New} (from {Carrier})",
            shipment.TrackingNumber, previousStatus, observation.Status, observation.Carrier);

        return true;
    }

    private static ShipmentStatusChangedEvent BuildEvent(
        TrackedShipment shipment,
        CarrierTrackingResult observation,
        CarrierStatus previousStatus) =>
        new(
            shipment.ShipmentId,
            shipment.TrackingNumber,
            shipment.TenantId,
            shipment.UserId,
            shipment.BuyerEmail,
            previousStatus.ToString(),
            observation.Status.ToString(),
            observation.Location,
            observation.Description,
            observation.OccurredAt);
}
