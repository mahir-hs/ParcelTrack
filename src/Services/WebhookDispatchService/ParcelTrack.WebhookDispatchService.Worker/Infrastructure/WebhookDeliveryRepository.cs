using ParcelTrack.WebhookDispatchService.Worker.Domain;

namespace ParcelTrack.WebhookDispatchService.Worker.Infrastructure;

public sealed class WebhookDeliveryRepository(WebhookDbContext context) : IWebhookDeliveryRepository
{
    public async Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        await context.WebhookDeliveries.AddAsync(delivery, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
