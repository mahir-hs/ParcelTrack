namespace ParcelTrack.TrackingService.Domain.Enums;

/// <summary>
/// ParcelTrack's normalised status vocabulary.
///
/// Every courier names its states differently — Pathao's "Assigned_for_Delivery" and
/// Steadfast's "delivered_approval_pending" describe the same reality in different words.
/// Adapters translate into this enum so the rest of the system never learns courier dialects.
///
/// The first six values line up with ShipmentService's ShipmentStatus. Returned and Unknown
/// exist only here: couriers report them, but they are not shipment lifecycle states.
/// </summary>
public enum CarrierStatus
{
    /// <summary>Courier reported a status this adapter does not recognise — logged, never acted on.</summary>
    Unknown = 0,

    Created = 1,
    InTransit = 2,
    OutForDelivery = 3,
    Failed = 4,
    Delivered = 5,
    Cancelled = 6,

    /// <summary>Parcel is on its way back to the merchant. No ShipmentStatus equivalent yet.</summary>
    Returned = 7
}
