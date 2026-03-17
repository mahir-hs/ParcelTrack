using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParcelTrack.ShipmentService.Infrastructure.Interfaces;
using ParcelTrack.ShipmentService.Infrastructure.Persistence;

namespace ParcelTrack.ShipmentService.Infrastructure.BackgroundServices;

/// <summary>
/// Polls outbox_messages every 5 seconds and publishes unprocessed messages to Kafka.
///
/// FOR UPDATE SKIP LOCKED ensures safe concurrent operation across multiple API instances:
/// - Each instance locks the rows it's processing
/// - Other instances skip those locked rows and take the next available batch
/// - No duplicate processing, no coordination service needed
///
/// Must create its own DI scope per batch — BackgroundService is singleton,
/// but ShipmentDbContext is scoped. Cannot inject DbContext directly.
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown — not an error
                break;
            }
            catch (Exception ex)
            {
                // Log and keep running — a single bad batch should not kill the processor
                _logger.LogError(ex, "OutboxProcessor encountered an unexpected error");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxProcessor stopped");
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
        var kafkaProducer = scope.ServiceProvider.GetRequiredService<IKafkaProducer>();

        // Transaction is required for FOR UPDATE SKIP LOCKED
        // The lock is held until the transaction commits or rolls back
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // FOR UPDATE SKIP LOCKED:
            // - FOR UPDATE: locks the selected rows against concurrent access
            // - SKIP LOCKED: skips rows locked by other transactions (other API instances)
            // Result: each instance processes a unique non-overlapping batch
            var messages = await dbContext.OutboxMessages
                .FromSqlRaw($"""
                    SELECT * FROM outbox_messages
                    WHERE processed_at IS NULL
                    ORDER BY created_at
                    LIMIT {BatchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);

            if (messages.Count == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            _logger.LogInformation("Processing {Count} outbox messages", messages.Count);

            foreach (var message in messages)
            {
                try
                {
                    await kafkaProducer.ProduceAsync(
                        message.Topic,
                        message.Type,
                        message.Payload,
                        cancellationToken);

                    message.MarkProcessed();
                }
                catch (Exception ex)
                {
                    // Don't fail the entire batch — mark this message as failed and move on
                    _logger.LogError(ex,
                        "Failed to publish outbox message {MessageId} (type: {Type}, attempt: {Attempt})",
                        message.Id, message.Type, message.AttemptCount + 1);

                    message.RecordFailure(ex.Message);
                }
            }

            // Commit: saves processed_at timestamps + failure records, then releases locks
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}