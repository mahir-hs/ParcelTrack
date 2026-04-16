namespace ParcelTrack.ShipmentService.API.Models;

/// <summary>
/// What the caller sends in the request body for POST /shipments.
/// Deliberately excludes TenantId and UserId — those come from the JWT via TenantContext.
/// </summary>
public sealed record CreateShipmentRequest
{
    public string TrackingNumber { get; init; } = string.Empty;
    public string CarrierType { get; init; } = string.Empty;
    public string? BuyerEmail { get; init; }
    public string? DestinationCity { get; init; }
}
