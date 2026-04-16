using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParcelTrack.ShipmentService.Infrastructure.Interfaces;

namespace ParcelTrack.ShipmentService.Infrastructure.Messaging;

/// <summary>
/// Used exclusively by OutboxProcessor to publish to Kafka.
/// Application-layer code never depends on this — it uses IEventProducer instead.
/// </summary>

public sealed class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
    {
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"]
                               ?? throw new InvalidOperationException("Kafka:BootstrapServers is not configured"),

            // Wait for all in-sync replicas to acknowledge — no data loss on broker failure
            Acks = Acks.All,

            // Prevents duplicate messages if the producer retries after a timeout
            EnableIdempotence = true,

            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task ProduceAsync(
        string topic,
        string type,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string>
        {
            // Key = event type — Kafka uses this for partition assignment
            // Same event type always goes to the same partition (ordering guarantee)
            Key = type,
            Value = payload,
            Headers = new Headers
            {
                { "event-type", Encoding.UTF8.GetBytes(type) }
            }
        };

        var result = await _producer.ProduceAsync(topic, message, cancellationToken);

        _logger.LogInformation(
            "Published {EventType} to {Topic} [partition {Partition}, offset {Offset}]",
            type, topic, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose() => _producer.Dispose();
}