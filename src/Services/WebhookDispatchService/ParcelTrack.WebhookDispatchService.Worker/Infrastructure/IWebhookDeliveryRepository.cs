using ParcelTrack.WebhookDispatchService.Worker.Domain;

namespace ParcelTrack.WebhookDispatchService.Worker.Infrastructure;

public interface IWebhookDeliveryRepository
{
    Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
