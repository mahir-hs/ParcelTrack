using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.Application.DTOs;

public sealed record ShipmentDto
{
    public Guid Id { get; init; }
    public string TrackingNumber { get; init; } = string.Empty;
    public CarrierType CarrierType { get; init; }
    public ShipmentStatus Status { get; init; }
    public string? BuyerEmail { get; init; }
    public int DeliveryAttempts { get; init; }
    public Guid UserId { get; init; }
    public Guid TenantId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyList<ShipmentEventDto> Events { get; init; } = [];
}