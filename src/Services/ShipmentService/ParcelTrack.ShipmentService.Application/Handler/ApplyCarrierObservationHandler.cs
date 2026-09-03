using Microsoft.Extensions.Logging;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.ShipmentService.Application.Commands;
using ParcelTrack.ShipmentService.Domain.Enums;
using ParcelTrack.ShipmentService.Domain.Exceptions;

namespace ParcelTrack.ShipmentService.Application.Handler;

/// <summary>
/// Applies a courier observation to the authoritative shipment.
///
/// This is what closes the loop between the carrier and the shipment record: TrackingService
/// reports what the courier said, and this decides whether it can be believed. The observation
/// goes through <see cref="UpdateShipmentStatusCommandHandler"/> like any API call, so the
/// state machine, the delivery-attempt cap, and the outbox all apply identically — a courier
/// gets no special authority to violate the domain's rules.
///
/// Every rejection is swallowed and logged rather than thrown. A Kafka consumer that dies on a
/// message it can never process will retry it forever and block the partition behind it; an
/// impossible transition is bad data, not a transient fault, and the right response is to
/// record it and move on.
/// </summary>
public sealed class ApplyCarrierObservationHandler(
    UpdateShipmentStatusCommandHandler updateStatus,
    ILogger<ApplyCarrierObservationHandler> logger)
{
    public async Task<CarrierObservationResult> HandleAsync(
        CarrierStatusObservedEvent @event,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapStatus(@event.ObservedStatus, out var newStatus))
        {
            // Returned and Unknown have no ShipmentStatus equivalent. Not an error — the
            // tracking log still records them, the shipment simply has nothing to change to.
            logger.LogInformation(
                "No shipment status maps to observed {ObservedStatus} for {TrackingNumber} — ignoring",
                @event.ObservedStatus, @event.TrackingNumber);
            return CarrierObservationResult.NotApplicable;
        }

        var command = new UpdateShipmentStatusCommand
        {
            ShipmentId = @event.ShipmentId,
            TenantId = @event.TenantId,
            NewStatus = newStatus,
            Description = @event.Description,
            Location = @event.Location ?? string.Empty
        };

        try
        {
            await updateStatus.Handle(command, cancellationToken);

            logger.LogInformation(
                "Applied {Carrier} observation to shipment {ShipmentId}: now {Status}",
                @event.Carrier, @event.ShipmentId, newStatus);

            return CarrierObservationResult.Applied;
        }
        catch (InvalidShipmentStatusTransitionException ex)
        {
            // Common and benign: the courier repeated a status we already applied, or reported
            // one out of order. The shipment is right and the observation is stale.
            logger.LogInformation(ex,
                "Ignoring impossible transition to {Status} for shipment {ShipmentId}",
                newStatus, @event.ShipmentId);
            return CarrierObservationResult.Rejected;
        }
        catch (ShipmentAlreadyTerminatedException ex)
        {
            logger.LogInformation(ex,
                "Ignoring observation for already-terminated shipment {ShipmentId}",
                @event.ShipmentId);
            return CarrierObservationResult.Rejected;
        }
        catch (MaxDeliveryAttemptsExceededException ex)
        {
            // Worth attention: the courier is still attempting delivery past the agreed cap.
            logger.LogWarning(ex,
                "Shipment {ShipmentId} exceeded the delivery attempt cap — courier is still trying",
                @event.ShipmentId);
            return CarrierObservationResult.Rejected;
        }
        catch (ShipmentNotFoundException ex)
        {
            // The shipment belongs to another tenant, or was deleted. Never retryable.
            logger.LogWarning(ex,
                "Observation for unknown shipment {ShipmentId} in tenant {TenantId}",
                @event.ShipmentId, @event.TenantId);
            return CarrierObservationResult.Rejected;
        }
    }

    /// <summary>
    /// Maps TrackingService's normalised vocabulary onto ShipmentStatus.
    ///
    /// The two enums share names for the six lifecycle states by design, so this is a parse
    /// rather than a lookup table — but Returned and Unknown exist only on the carrier side
    /// and must not resolve to anything.
    /// </summary>
    private static bool TryMapStatus(string observedStatus, out ShipmentStatus status)
    {
        status = default;

        if (string.Equals(observedStatus, "Returned", StringComparison.OrdinalIgnoreCase)
            || string.Equals(observedStatus, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Enum.TryParse(observedStatus, ignoreCase: true, out status)
               && Enum.IsDefined(status);
    }
}

public enum CarrierObservationResult
{
    /// <summary>The shipment moved and an authoritative event was published.</summary>
    Applied,

    /// <summary>The domain refused the transition. Logged, not retried.</summary>
    Rejected,

    /// <summary>The observed status has no shipment equivalent.</summary>
    NotApplicable
}
