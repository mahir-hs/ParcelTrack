namespace ParcelTrack.ShipmentService.Application.Queries;

public sealed record GetShipmentByIdQuery
{
    public Guid ShipmentId { get; init; }
    public Guid TenantId { get; init; }
}
