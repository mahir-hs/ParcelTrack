using System.Text.Json;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Infrastructure.Persistence;
using ParcelTrack.ShipmentService.Infrastructure.Persistence.Outbox;

namespace ParcelTrack.ShipmentService.Infrastructure.Messaging;

/// <summary>
/// Implements IEventProducer by writing to outbox_messages in the same DbContext
/// that the calling handler is using. No Kafka call happens here.
///
/// Flow:
///   Handler → OutboxEventProducer.PublishAsync() → EF Core tracks OutboxMessage
///   Handler → IUnitOfWork.SaveChangesAsync()      → shipment + outbox_message committed atomically
///   OutboxProcessor (background)                  → reads outbox_messages → publishes to Kafka
///
/// The handler doesn't know or care that Kafka isn't called immediately.
/// </summary>
public sealed class OutboxEventProducer : IEventProducer
{
    private readonly ShipmentDbContext _context;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OutboxEventProducer(ShipmentDbContext context)
    {
        _context = context;
    }

    public Task PublishAsync<T>(string topic, T @event, CancellationToken cancellationToken = default)
        where T : class
    {
        var type = typeof(T).FullName ?? typeof(T).Name;
        var payload = JsonSerializer.Serialize(@event, JsonOptions);

        var message = OutboxMessage.Create(topic, type, payload);

        // No I/O — EF Core change tracker holds this until SaveChangesAsync is called
        _context.OutboxMessages.Add(message);

        return Task.CompletedTask;
    }
}