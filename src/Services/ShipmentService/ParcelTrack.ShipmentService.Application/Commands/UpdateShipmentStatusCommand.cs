using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.Application.Commands;

public sealed record UpdateShipmentStatusCommand
{
    public Guid ShipmentId { get; init; }
    public Guid TenantId { get; init; }
    public ShipmentStatus NewStatus { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
}