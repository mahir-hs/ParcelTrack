namespace ParcelTrack.Shared.Messaging;

/// <summary>
/// Configuration for a Kafka consumer, bound from the "Kafka:Consumer" configuration section.
/// </summary>
public sealed class KafkaConsumerOptions
{
    public const string SectionName = "Kafka:Consumer";

    /// <summary>Comma/host:port list of bootstrap servers (e.g. "localhost:9092").</summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Consumer group id — unique per service so offsets are tracked independently.</summary>
    public string GroupId { get; set; } = "parceltrack-consumer";

    /// <summary>Topics to subscribe to.</summary>
    public string[] Topics { get; set; } = [];
}
