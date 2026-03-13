using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.Application.DTOs;

public sealed record ShipmentEventDto
{
    public Guid Id { get; init; }
    public Guid ShipmentId { get; init; }
    public ShipmentStatus Status { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? Location { get; init; }
    public DateTime OccurredAt { get; init; }
}
