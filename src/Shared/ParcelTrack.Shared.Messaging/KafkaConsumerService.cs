using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ParcelTrack.Shared.Messaging;

/// <summary>
/// Base class for all ParcelTrack event consumers.
///
/// Responsibilities:
///   - Connect to Kafka and subscribe to the configured topics
///   - For each message: read the "event-type" header, resolve it to a CLR type via
///     <see cref="EventTypes"/>, JSON-deserialize the payload, and hand it to
///     <see cref="HandleAsync"/> implemented by the derived service
///   - Commit the offset only after the handler returns (at-least-once delivery)
///   - Isolate handler failures so one bad message never takes down the consumer
///
/// Unknown event types are skipped (logged) and the offset is still committed so the
/// consumer never gets stuck on a poison message.
/// </summary>
public abstract class KafkaConsumerService : BackgroundService
{
    private readonly ConsumerConfig _config;
    private readonly string[] _topics;
    private readonly ILogger<KafkaConsumerService> _logger;

    protected KafkaConsumerService(
        IOptions<KafkaConsumerOptions> options,
        ILogger<KafkaConsumerService> logger)
    {
        var o = options.Value;
        _config = new ConsumerConfig
        {
            BootstrapServers = o.BootstrapServers,
            GroupId = o.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            // Heartbeat/timeout tuned for local dev — safe to stay on a partition
            SessionTimeoutMs = 10000,
            MaxPollIntervalMs = 120000
        };
        _topics = o.Topics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Kafka consumer '{Group}' starting — subscribing to: {Topics}",
            _config.GroupId, string.Join(", ", _topics));

        using var consumer = new ConsumerBuilder<Ignore, string>(_config).Build();
        consumer.Subscribe(_topics);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    await ProcessAsync(result, stoppingToken);
                    consumer.Commit(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in Kafka consumer loop");
                }
            }
        }
        finally
        {
            consumer.Close();
            _logger.LogInformation("Kafka consumer '{Group}' stopped", _config.GroupId);
        }
    }

    private async Task ProcessAsync(ConsumeResult<Ignore, string> result, CancellationToken ct)
    {
        var typeHeader = result.Message.Headers
            .FirstOrDefault(h => h.Key == "event-type")?
            .GetValueBytes();

        if (typeHeader is null)
        {
            _logger.LogWarning("Message on {Topic} had no event-type header; skipping", result.Topic);
            return;
        }

        var eventType = Encoding.UTF8.GetString(typeHeader);

        if (!EventTypes.TryResolve(eventType, out var type))
        {
            _logger.LogWarning("Unknown event type '{EventType}'; skipping", eventType);
            return;
        }

        try
        {
            var payload = JsonSerializer.Deserialize(result.Message.Value, type);
            if (payload is null)
            {
                _logger.LogWarning("Failed to deserialize payload for '{EventType}'; skipping", eventType);
                return;
            }

            await HandleAsync(result.Topic, eventType, payload, ct);
        }
        catch (Exception ex)
        {
            // Don't rethrow — a single bad message must not kill the consumer.
            _logger.LogError(ex, "Handler failed for event '{EventType}' on {Topic}", eventType, result.Topic);
        }
    }

    /// <summary>
    /// Dispatch a deserialized event. Implementations typically pattern-match on
    /// <paramref name="eventType"/> / cast <paramref name="payload"/> to the known record types.
    /// </summary>
    protected abstract Task HandleAsync(
        string topic,
        string eventType,
        object payload,
        CancellationToken cancellationToken);
}
