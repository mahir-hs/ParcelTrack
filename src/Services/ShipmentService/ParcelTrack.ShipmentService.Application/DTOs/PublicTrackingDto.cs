namespace ParcelTrack.ShipmentService.Application.DTOs;

public sealed record PublicTrackingDto
{
    public string TrackingNumber { get; init; } = string.Empty;
    public string Carrier { get; init; } = string.Empty;
    public string CurrentStatus { get; init; } = string.Empty;
    public string? DestinationCity { get; init; }
    public List<PublicTrackingEventDto> Events { get; init; } = [];
}
