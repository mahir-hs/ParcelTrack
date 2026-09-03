using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParcelTrack.Shared.Contracts;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.ShipmentService.Application.Handler;
using ParcelTrack.ShipmentService.Application.Interfaces;

namespace ParcelTrack.ShipmentService.Infrastructure.BackgroundServices;

/// <summary>
/// Consumes courier observations from TrackingService and applies them to the shipment.
///
/// This is the only place ShipmentService acts without a request behind it, and the two
/// consequences both live here:
///
/// - Each message gets its own DI scope, so the DbContext and unit of work are per-message
///   rather than shared for the lifetime of the process.
/// - The tenant is stated explicitly from the message before any handler runs. Every shipment
///   query is filtered by tenant, and there is no JWT here to take one from.
///
/// Offsets are committed manually and only after the message has been fully handled, so a
/// crash mid-processing replays the message rather than losing it. Applying the same
/// observation twice is harmless: the second attempt is an impossible transition and the
/// domain rejects it.
/// </summary>
public sealed class CarrierObservationConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CarrierObservationConsumer> logger) : BackgroundService
{
    private const string ConsumerGroup = "shipment-service-carrier-observations";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(Topics.CarrierStatusObserved);

        logger.LogInformation(
            "Listening for carrier observations on {Topic}", Topics.CarrierStatusObserved);

        // Consume blocks, so the loop is pushed off the startup path to let the host finish booting.
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null)
                    continue;

                await ProcessAsync(result.Message.Value, stoppingToken);
                consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Back off briefly rather than spinning on a broker that is unreachable.
                logger.LogError(ex, "Error consuming carrier observation");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
        logger.LogInformation("Carrier observation consumer stopped");
    }

    private async Task ProcessAsync(string payload, CancellationToken cancellationToken)
    {
        CarrierStatusObservedEvent? @event;
        try
        {
            @event = JsonSerializer.Deserialize<CarrierStatusObservedEvent>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            // Unreadable message. Committing the offset drops it deliberately — retrying
            // forever would block every observation queued behind it.
            logger.LogError(ex, "Discarding unreadable carrier observation");
            return;
        }

        if (@event is null || @event.TenantId == Guid.Empty)
        {
            logger.LogWarning("Discarding carrier observation with no tenant");
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();

        // Must happen before anything touches the database — the shipment query filter reads it.
        scope.ServiceProvider
            .GetRequiredService<ITenantContextSetter>()
            .SetContext(@event.TenantId, @event.UserId);

        await scope.ServiceProvider
            .GetRequiredService<ApplyCarrierObservationHandler>()
            .HandleAsync(@event, cancellationToken);
    }
}
