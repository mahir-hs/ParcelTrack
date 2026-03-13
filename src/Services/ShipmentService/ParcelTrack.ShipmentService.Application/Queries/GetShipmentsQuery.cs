using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.Application.Queries;

public sealed record GetShipmentsQuery
{
    public Guid TenantId { get; set; }
    public int Page {  get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public ShipmentStatus? StatusFilter { get; set; } = null;
}
