namespace ParcelTrack.ShipmentService.Infrastructure.Persistence.Outbox;

/// <summary>
/// Represents a pending event that must be published to Kafka.
/// Written in the same DB transaction as the domain aggregate — guarantees
/// no event is lost if Kafka is unavailable at the time of the write.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Topic { get; private set; } = default!;
    public string Type { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int AttemptCount { get; private set; }

    // Required by EF Core
    private OutboxMessage() { }

    public static OutboxMessage Create(string topic, string type, string payload) => new()
    {
        Id = Guid.NewGuid(),
        Topic = topic,
        Type = type,
        Payload = payload,
        CreatedAt = DateTime.UtcNow,
        AttemptCount = 0
    };

    public void MarkProcessed() => ProcessedAt = DateTime.UtcNow;

    public void RecordFailure(string error)
    {
        Error = error;
        AttemptCount++;
    }
}