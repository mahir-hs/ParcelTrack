using ParcelTrack.TrackingService.Domain.Enums;

namespace ParcelTrack.TrackingService.Domain.Entities;

/// <summary>
/// A shipment the polling worker is watching, plus the last status the courier reported.
///
/// TrackingRecord is an append-only history; this is the current-state registry that drives
/// polling. Keeping them apart means the poller reads one indexed row per parcel instead of
/// reducing an event stream on every cycle.
///
/// LastKnownStatus is the change-detection anchor: the courier is polled repeatedly and
/// almost always answers with the same status, so an event is published only when the answer
/// actually differs from what is stored here.
/// </summary>
public sealed class TrackedShipment
{
    /// <summary>Statuses after which a courier has nothing further to tell us.</summary>
    private static readonly CarrierStatus[] TerminalStatuses =
        [CarrierStatus.Delivered, CarrierStatus.Cancelled, CarrierStatus.Returned];

    public Guid Id { get; private set; }
    public Guid ShipmentId { get; private set; }
    public string TrackingNumber { get; private set; } = string.Empty;
    public CarrierType CarrierType { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Carried so the poller can publish an event rich enough to notify the buyer.</summary>
    public string? BuyerEmail { get; private set; }

    public CarrierStatus LastKnownStatus { get; private set; }
    public DateTime? LastPolledAt { get; private set; }

    /// <summary>False once the parcel reaches a terminal state — the poller skips these.</summary>
    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private TrackedShipment() { }

    public static TrackedShipment Create(
        Guid shipmentId,
        string trackingNumber,
        CarrierType carrierType,
        Guid tenantId,
        Guid userId,
        string? buyerEmail,
        DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        ShipmentId = shipmentId,
        TrackingNumber = trackingNumber,
        CarrierType = carrierType,
        TenantId = tenantId,
        UserId = userId,
        BuyerEmail = buyerEmail,
        LastKnownStatus = CarrierStatus.Created,
        IsActive = true,
        CreatedAt = createdAt,
        UpdatedAt = createdAt
    };

    /// <summary>
    /// Records what the courier just reported.
    ///
    /// Returns true only when this is genuinely new information worth publishing — the status
    /// differs from the last known one and is not <see cref="CarrierStatus.Unknown"/>. Polling
    /// is inherently repetitive; without this guard every cycle would fan out duplicate
    /// notifications to buyers.
    /// </summary>
    public bool TryRecordObservedStatus(CarrierStatus observed, DateTime observedAt)
    {
        LastPolledAt = observedAt;
        UpdatedAt = observedAt;

        // An unmapped courier status is not evidence of a change — never act on it.
        if (observed is CarrierStatus.Unknown || observed == LastKnownStatus)
            return false;

        LastKnownStatus = observed;

        if (TerminalStatuses.Contains(observed))
            IsActive = false;

        return true;
    }

    /// <summary>Stops polling this shipment — used when ShipmentService reports a terminal state first.</summary>
    public void Deactivate(DateTime deactivatedAt)
    {
        IsActive = false;
        UpdatedAt = deactivatedAt;
    }

    /// <summary>Applies a status ParcelTrack already knows about, without treating it as a courier observation.</summary>
    public void SyncStatus(CarrierStatus status, DateTime occurredAt)
    {
        if (status is not CarrierStatus.Unknown)
            LastKnownStatus = status;

        UpdatedAt = occurredAt;

        if (TerminalStatuses.Contains(status))
            IsActive = false;
    }
}
