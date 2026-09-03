using ParcelTrack.WebhookDispatchService.Worker.Domain;

namespace ParcelTrack.WebhookDispatchService.Worker.Infrastructure;

public interface IWebhookSubscriptionRepository
{
    Task<IReadOnlyList<WebhookSubscription>> GetActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookSubscription>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<WebhookSubscription?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
