using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using ParcelTrack.Shared.Contracts;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.Handlers;
using ParcelTrack.TrackingService.Worker.Settings;

namespace ParcelTrack.TrackingService.Worker;

public sealed class Worker(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaSettings> kafkaOptions,
    ILogger<Worker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.Value.BootstrapServers,
            GroupId = "tracking-service",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe([Topics.ShipmentCreated, Topics.ShipmentStatusChanged]);

        logger.LogInformation("TrackingService Kafka consumer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null) continue;

                await using var scope = scopeFactory.CreateAsyncScope();
                await ProcessAsync(scope, result.Topic, result.Message.Value, stoppingToken);
                consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing Kafka message");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
        logger.LogInformation("TrackingService Kafka consumer stopped");
    }

    private async Task ProcessAsync(IServiceScope scope, string topic, string payload, CancellationToken cancellationToken)
    {
        if (topic == Topics.ShipmentCreated)
        {
            var @event = JsonSerializer.Deserialize<ShipmentCreatedEvent>(payload, JsonOptions);
            if (@event is not null)
                await scope.ServiceProvider.GetRequiredService<ShipmentCreatedHandler>()
                    .HandleAsync(@event, cancellationToken);
        }
        else if (topic == Topics.ShipmentStatusChanged)
        {
            var @event = JsonSerializer.Deserialize<ShipmentStatusChangedEvent>(payload, JsonOptions);
            if (@event is not null)
                await scope.ServiceProvider.GetRequiredService<ShipmentStatusChangedHandler>()
                    .HandleAsync(@event, cancellationToken);
        }
    }
}
