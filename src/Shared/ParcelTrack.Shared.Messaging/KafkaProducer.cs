using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ParcelTrack.Shared.Messaging;

/// <summary>
/// Generic Kafka producer used by workers to publish dead-letter events
/// (e.g. notification.failed, webhook.failed). Singleton — one producer per process.
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
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task ProduceAsync(
        string topic,
        string eventType,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string>
        {
            Key = eventType,
            Value = payload,
            Headers = new Headers { { "event-type", Encoding.UTF8.GetBytes(eventType) } }
        };

        var result = await _producer.ProduceAsync(topic, message, cancellationToken);
        _logger.LogInformation(
            "Published {EventType} to {Topic} [partition {Partition}, offset {Offset}]",
            eventType, topic, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose() => _producer.Dispose();
}
