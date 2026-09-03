using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using ParcelTrack.Shared.Contracts;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.Interfaces;

namespace ParcelTrack.TrackingService.Infrastructure.Messaging;

/// <summary>
/// Publishes courier-observed status changes to Kafka.
///
/// No outbox here, unlike ShipmentService. The outbox exists to keep an event atomic with a
/// database write; a poll result is an observation of the outside world, and if publishing
/// fails the next cycle simply observes the same status again and retries. Re-reading the
/// courier is cheaper and simpler than a second outbox table.
///
/// Keyed by tracking number so all events for one parcel land on the same partition and are
/// therefore consumed in order.
///
/// Scoped, wrapping a singleton IProducer — Confluent's guidance is one producer per process,
/// reused. Disposal and flushing belong to the producer's own registration, not to this class.
/// </summary>
public sealed class KafkaCarrierEventPublisher : ICarrierEventPublisher
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaCarrierEventPublisher> _logger;

    public KafkaCarrierEventPublisher(
        IProducer<string, string> producer,
        ILogger<KafkaCarrierEventPublisher> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    public async Task PublishObservationAsync(
        CarrierStatusObservedEvent @event,
        CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string>
        {
            Key = @event.TrackingNumber,
            Value = JsonSerializer.Serialize(@event)
        };

        try
        {
            var result = await _producer.ProduceAsync(Topics.CarrierStatusObserved, message, cancellationToken);

            _logger.LogInformation(
                "Published {Topic} for {TrackingNumber} to partition {Partition} at offset {Offset}",
                Topics.CarrierStatusObserved, @event.TrackingNumber,
                result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex,
                "Failed to publish observation for {TrackingNumber} — the next poll will retry",
                @event.TrackingNumber);
            throw;
        }
    }
}
