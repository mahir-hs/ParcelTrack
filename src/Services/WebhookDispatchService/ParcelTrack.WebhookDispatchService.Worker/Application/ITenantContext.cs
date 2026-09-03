namespace ParcelTrack.WebhookDispatchService.Worker.Application;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid UserId { get; }
}
