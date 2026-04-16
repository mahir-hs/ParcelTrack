namespace ParcelTrack.ShipmentService.Application.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid UserId { get; }
}
