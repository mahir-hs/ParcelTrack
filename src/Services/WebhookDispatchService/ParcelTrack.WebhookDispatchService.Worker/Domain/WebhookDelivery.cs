namespace ParcelTrack.WebhookDispatchService.Worker.Domain;

public sealed class WebhookDelivery
{
    public Guid Id { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public int AttemptCount { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool IsSuccessful { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }

    public const int MaxAttempts = 3;

    private WebhookDelivery() { }

    public static WebhookDelivery Create(Guid subscriptionId, string eventType, string payload) => new()
    {
        Id = Guid.NewGuid(),
        SubscriptionId = subscriptionId,
        EventType = eventType,
        Payload = payload,
        AttemptCount = 0,
        IsSuccessful = false,
        CreatedAt = DateTime.UtcNow
    };

    public void RecordSuccess(int statusCode)
    {
        AttemptCount++;
        ResponseStatusCode = statusCode;
        IsSuccessful = true;
        DeliveredAt = DateTime.UtcNow;
    }

    public void RecordFailure(int? statusCode, string error)
    {
        AttemptCount++;
        ResponseStatusCode = statusCode;
        ErrorMessage = error;
    }

    public bool IsExhausted => AttemptCount >= MaxAttempts && !IsSuccessful;
}
