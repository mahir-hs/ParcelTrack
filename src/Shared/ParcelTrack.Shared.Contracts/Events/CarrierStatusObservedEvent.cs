namespace ParcelTrack.Shared.Contracts.Events;

/// <summary>
/// Published to 'carrier.status.observed' when a courier reports movement — discovered by
/// polling or pushed by the courier's webhook. Consumed by ShipmentService.
///
/// Deliberately distinct from <see cref="ShipmentStatusChangedEvent"/>, and the distinction
/// carries the whole design:
///
/// - This is an <i>observation</i>. A courier claims something happened. It has not been
///   checked against the shipment's state machine and may be impossible (a parcel cannot go
///   straight from Created to Delivered) or already known.
/// - ShipmentStatusChangedEvent is a <i>decision</i>. ShipmentService validated the transition,
///   applied it, and is telling the rest of the system what is now true.
///
/// Keeping them separate is also what stops the loop: if the poller published
/// ShipmentStatusChangedEvent directly, ShipmentService would apply it and publish the same
/// event again, forever.
/// </summary>
public sealed record CarrierStatusObservedEvent(
    Guid ShipmentId,
    string TrackingNumber,
    Guid TenantId,
    Guid UserId,
    string Carrier,

    /// <summary>ParcelTrack's normalised status name, e.g. "OutForDelivery".</summary>
    string ObservedStatus,

    /// <summary>The courier's own wording, kept for audit and for diagnosing bad mappings.</summary>
    string RawStatus,

    string Description,
    string? Location,
    DateTime OccurredAt);
