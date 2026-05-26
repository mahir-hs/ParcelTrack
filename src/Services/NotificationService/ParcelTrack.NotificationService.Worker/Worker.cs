using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using ParcelTrack.NotificationService.Application.Handlers;
using ParcelTrack.NotificationService.Worker.Settings;
using ParcelTrack.Shared.Contracts;
using ParcelTrack.Shared.Contracts.Events;

namespace ParcelTrack.NotificationService.Worker;

/// <summary>
/// Consumes shipment.created and shipment.status.changed from Kafka
/// and dispatches to the appropriate application handler.
/// </summary>
public sealed class Worker(
    ShipmentCreatedHandler createdHandler,
    ShipmentStatusChangedHandler statusChangedHandler,
    IOptions<KafkaSettings> kafkaOptions,
    ILogger<Worker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = kafkaOptions.Value;

        var config = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = "notification-service",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false  // manual commit — only after successful processing
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        consumer.Subscribe([Topics.ShipmentCreated, Topics.ShipmentStatusChanged]);

        logger.LogInformation("NotificationService Kafka consumer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);

                if (result?.Message?.Value is null)
                    continue;

                await ProcessMessageAsync(result.Topic, result.Message.Value, stoppingToken);

                consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error processing Kafka message");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
        logger.LogInformation("NotificationService Kafka consumer stopped");
    }

    private async Task ProcessMessageAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        if (topic == Topics.ShipmentCreated)
        {
            var @event = JsonSerializer.Deserialize<ShipmentCreatedEvent>(payload, JsonOptions);
            if (@event is not null)
                await createdHandler.HandleAsync(@event, cancellationToken);
        }
        else if (topic == Topics.ShipmentStatusChanged)
        {
            var @event = JsonSerializer.Deserialize<ShipmentStatusChangedEvent>(payload, JsonOptions);
            if (@event is not null)
                await statusChangedHandler.HandleAsync(@event, cancellationToken);
        }
        else
        {
            logger.LogWarning("Received message from unexpected topic: {Topic}", topic);
        }
    }
}
