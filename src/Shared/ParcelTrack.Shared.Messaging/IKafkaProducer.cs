namespace ParcelTrack.Shared.Messaging;

/// <summary>
/// Minimal produce contract used by workers to publish to dead-letter / failed topics.
/// </summary>
public interface IKafkaProducer
{
    Task ProduceAsync(
        string topic,
        string eventType,
        string payload,
        CancellationToken cancellationToken = default);
}
