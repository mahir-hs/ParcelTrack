using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.Application.DTOs;

public sealed record ShipmentSummaryDto
{
    public Guid Id { get; init; }
    public string TrackingNumber { get; init; } = string.Empty;
    public CarrierType CarrierType { get; init; }
    public ShipmentStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
