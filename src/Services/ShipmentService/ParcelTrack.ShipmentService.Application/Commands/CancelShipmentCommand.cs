namespace ParcelTrack.ShipmentService.Application.Commands;

public sealed record CancelShipmentCommand
{    
    public Guid ShipmentId { get; init; }
    public Guid TenantId { get; init; }
    public Guid RequestingUserId { get; init; }
    public string Reason { get; init; } = string.Empty;
}