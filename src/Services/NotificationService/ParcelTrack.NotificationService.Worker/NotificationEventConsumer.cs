using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParcelTrack.NotificationService.Application.Handlers;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.Shared.Messaging;

namespace ParcelTrack.NotificationService.Worker;

/// <summary>
/// Consumes shipment.status.changed and triggers customer notifications.
/// </summary>
public sealed class NotificationEventConsumer(
    IOptions<KafkaConsumerOptions> options,
    ILogger<KafkaConsumerService> logger,
    ShipmentStatusChangedEventHandler statusHandler)
    : KafkaConsumerService(options, logger)
{
    protected override async Task HandleAsync(
        string topic,
        string eventType,
        object payload,
        CancellationToken cancellationToken)
    {
        if (payload is ShipmentStatusChangedEvent e)
            await statusHandler.Handle(e, cancellationToken);
    }
}
