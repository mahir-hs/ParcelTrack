using Microsoft.EntityFrameworkCore;
using ParcelTrack.WebhookDispatchService.Worker.Domain;

namespace ParcelTrack.WebhookDispatchService.Worker.Infrastructure;

public sealed class WebhookSubscriptionRepository(WebhookDbContext context) : IWebhookSubscriptionRepository
{
    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await context.WebhookSubscriptions
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .ToListAsync(cancellationToken);
    }
}
