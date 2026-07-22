using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParcelTrack.Shared.Contracts;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.Shared.Messaging;
using ParcelTrack.WebhookDispatchService.Worker.Models;

namespace ParcelTrack.WebhookDispatchService.Worker;

/// <summary>
/// Forwards shipment events to configured subscriber URLs as JSON POSTs, with
/// exponential-backoff retries. When all retries are exhausted the delivery is reported
/// on the 'webhook.failed' dead-letter topic.
/// </summary>
public sealed class WebhookDispatcher(
    IHttpClientFactory httpClientFactory,
    IKafkaProducer kafkaProducer,
    IOptions<WebhookOptions> options,
    ILogger<WebhookDispatcher> logger)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IKafkaProducer _kafkaProducer = kafkaProducer;
    private readonly WebhookOptions _options = options.Value;
    private readonly ILogger<WebhookDispatcher> _logger = logger;

    public async Task DispatchAsync(string topic, object payload, CancellationToken cancellationToken)
    {
        var subs = _options.Subscriptions
            .Where(s => s.Events.Contains("*") || s.Events.Contains(topic))
            .ToList();

        if (subs.Count == 0)
            return;

        var json = JsonSerializer.Serialize(payload);

        foreach (var sub in subs)
        {
            var delivered = await TryPostAsync(sub, json, cancellationToken);
            if (delivered)
                continue;

            _logger.LogWarning("Webhook {Name} exhausted retries — publishing to dead-letter", sub.Name);
            var failed = new WebhookFailedEvent(
                Guid.NewGuid(), sub.Name, sub.Url, topic, "Exhausted delivery retries", _options.MaxAttempts, DateTime.UtcNow);

            await _kafkaProducer.ProduceAsync(
                Topics.WebhookFailed,
                typeof(WebhookFailedEvent).FullName!,
                JsonSerializer.Serialize(failed),
                cancellationToken);
        }
    }

    private async Task<bool> TryPostAsync(
        WebhookOptions.Subscription sub,
        string json,
        CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("webhook");

        var maxAttempts = Math.Max(1, _options.MaxAttempts);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(sub.Url, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Webhook {Name} delivered to {Url} (attempt {Attempt})",
                        sub.Name, sub.Url, attempt);
                    return true;
                }

                _logger.LogWarning("Webhook {Name} -> {Url} returned {StatusCode} (attempt {Attempt})",
                    sub.Name, sub.Url, response.StatusCode, attempt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Webhook {Name} -> {Url} attempt {Attempt} threw", sub.Name, sub.Url, attempt);
            }

            if (attempt < maxAttempts)
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(Math.Max(1, _options.RetryDelayBaseSeconds), attempt)), cancellationToken);
        }

        return false;
    }
}
