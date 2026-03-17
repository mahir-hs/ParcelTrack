namespace ParcelTrack.ShipmentService.API.Models;

/// <summary>
/// What the caller sends for DELETE /shipments/{id}.
/// </summary>
public sealed record CancelShipmentRequest
{
    public string Reason { get; init; } = string.Empty;
}
