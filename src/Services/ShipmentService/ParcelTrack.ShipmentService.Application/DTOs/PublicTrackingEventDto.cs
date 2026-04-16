namespace ParcelTrack.ShipmentService.Application.DTOs;

public sealed record PublicTrackingEventDto
{
    public string Status { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Location { get; init; }
    public DateTime OccurredAt { get; init; }
}
