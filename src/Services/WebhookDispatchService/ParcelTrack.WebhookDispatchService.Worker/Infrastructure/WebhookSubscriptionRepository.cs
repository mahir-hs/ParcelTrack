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

    public async Task<IReadOnlyList<WebhookSubscription>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await context.WebhookSubscriptions
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WebhookSubscription?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await context.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId, cancellationToken);
    }

    public async Task AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default)
    {
        await context.WebhookSubscriptions.AddAsync(subscription, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
