namespace ParcelTrack.ShipmentService.Application.Interfaces;

/// <summary>
/// Kafka event publishing contract.
/// Implemented in the Infrastructure layer using Confluent.Kafka (Week 6).
/// </summary>
public interface IEventProducer
{
    Task PublishAsync<TEvent>(string topic, TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class;
}
