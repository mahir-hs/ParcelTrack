using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParcelTrack.Shared.Contracts;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.WebhookDispatchService.Worker.Domain;
using ParcelTrack.WebhookDispatchService.Worker.Infrastructure;

namespace ParcelTrack.WebhookDispatchService.Worker.Application;

public sealed class WebhookDispatchHandler(
    IWebhookSubscriptionRepository subscriptions,
    IWebhookDeliveryRepository deliveries,
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookDispatchHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task HandleAsync(ShipmentStatusChangedEvent @event, CancellationToken cancellationToken = default)
    {
        var activeSubscriptions = await subscriptions.GetActiveByTenantAsync(@event.TenantId, cancellationToken);

        if (activeSubscriptions.Count == 0)
        {
            logger.LogDebug("No active webhook subscriptions for tenant {TenantId}", @event.TenantId);
            return;
        }

        var payload = JsonSerializer.Serialize(@event, JsonOptions);

        foreach (var subscription in activeSubscriptions)
        {
            await DispatchAsync(subscription, payload, cancellationToken);
        }
    }

    private async Task DispatchAsync(WebhookSubscription subscription, string payload, CancellationToken cancellationToken)
    {
        var delivery = WebhookDelivery.Create(subscription.Id, "shipment.status.changed", payload);
        await deliveries.AddAsync(delivery, cancellationToken);

        var client = httpClientFactory.CreateClient("webhook");

        for (var attempt = 0; attempt < WebhookDelivery.MaxAttempts; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, subscription.TargetUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                // Sign the payload if the subscription has a secret
                if (!string.IsNullOrEmpty(subscription.Secret))
                {
                    var signature = ComputeSignature(payload, subscription.Secret);
                    request.Headers.Add("X-ParcelTrack-Signature", signature);
                }

                var response = await client.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    delivery.RecordSuccess((int)response.StatusCode);
                    await deliveries.SaveChangesAsync(cancellationToken);

                    logger.LogInformation(
                        "Webhook delivered to {Url} for subscription {SubscriptionId} — HTTP {Status}",
                        subscription.TargetUrl, subscription.Id, (int)response.StatusCode);
                    return;
                }

                delivery.RecordFailure((int)response.StatusCode, $"HTTP {(int)response.StatusCode}");
                logger.LogWarning(
                    "Webhook attempt {Attempt} failed for subscription {SubscriptionId} — HTTP {Status}",
                    attempt + 1, subscription.Id, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                delivery.RecordFailure(null, ex.Message);
                logger.LogWarning(ex,
                    "Webhook attempt {Attempt} failed for subscription {SubscriptionId}",
                    attempt + 1, subscription.Id);
            }

            if (!delivery.IsExhausted)
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken); // exponential backoff
        }

        await deliveries.SaveChangesAsync(cancellationToken);

        logger.LogError(
            "Webhook delivery exhausted after {Max} attempts for subscription {SubscriptionId} — publishing to {Topic}",
            WebhookDelivery.MaxAttempts, subscription.Id, Topics.WebhookFailed);
    }

    private static string ComputeSignature(string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, data);
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
