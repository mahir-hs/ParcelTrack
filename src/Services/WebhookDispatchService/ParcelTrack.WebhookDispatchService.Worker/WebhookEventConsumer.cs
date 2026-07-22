using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParcelTrack.Shared.Messaging;
using ParcelTrack.WebhookDispatchService.Worker.Models;

namespace ParcelTrack.WebhookDispatchService.Worker;

/// <summary>
/// Consumes shipment lifecycle events and fans them out to configured webhook subscribers.
/// </summary>
public sealed class WebhookEventConsumer(
    IOptions<KafkaConsumerOptions> options,
    ILogger<KafkaConsumerService> logger,
    WebhookDispatcher dispatcher)
    : KafkaConsumerService(options, logger)
{
    private readonly WebhookDispatcher _dispatcher = dispatcher;

    protected override async Task HandleAsync(
        string topic,
        string eventType,
        object payload,
        CancellationToken cancellationToken)
    {
        await _dispatcher.DispatchAsync(topic, payload, cancellationToken);
    }
}
