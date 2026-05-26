using ParcelTrack.WebhookDispatchService.Worker.Domain;

namespace ParcelTrack.WebhookDispatchService.Worker.Infrastructure;

public interface IWebhookSubscriptionRepository
{
    Task<IReadOnlyList<WebhookSubscription>> GetActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
