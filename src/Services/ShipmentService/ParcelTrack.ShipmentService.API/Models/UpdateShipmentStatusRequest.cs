namespace ParcelTrack.ShipmentService.API.Models;

/// <summary>
/// What the caller sends for PUT /shipments/{id}/status.
/// Carrier adapters and internal services use this — not end users directly.
/// </summary>
public sealed record UpdateShipmentStatusRequest
{
    public string NewStatus { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Location { get; init; }
}
