using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.Shared.Messaging;
using ParcelTrack.TrackingService.Application.Handlers;

namespace ParcelTrack.TrackingService.Worker;

/// <summary>
/// Consumes shipment lifecycle events and projects them into the tracking read model.
/// </summary>
public sealed class TrackingEventConsumer(
    IOptions<KafkaConsumerOptions> options,
    ILogger<KafkaConsumerService> logger,
    ShipmentCreatedEventHandler createdHandler,
    ShipmentStatusChangedEventHandler statusHandler)
    : KafkaConsumerService(options, logger)
{
    protected override async Task HandleAsync(
        string topic,
        string eventType,
        object payload,
        CancellationToken cancellationToken)
    {
        switch (payload)
        {
            case ShipmentCreatedEvent e:
                await createdHandler.Handle(e, cancellationToken);
                break;
            case ShipmentStatusChangedEvent e:
                await statusHandler.Handle(e, cancellationToken);
                break;
        }
    }
}
