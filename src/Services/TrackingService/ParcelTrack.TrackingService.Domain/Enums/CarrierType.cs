namespace ParcelTrack.TrackingService.Domain.Enums;

/// <summary>
/// Couriers ParcelTrack can talk to. Mirrors the ShipmentService enum by name —
/// deliberately duplicated rather than shared, so the two services can version independently.
/// </summary>
public enum CarrierType
{
    Steadfast = 1,
    Pathao = 2,
    Redx = 3
}
