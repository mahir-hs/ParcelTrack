namespace ParcelTrack.Shared.Contracts.Events;

/// <summary>
/// Published to 'webhook.failed' when an outbound webhook delivery exhausts its retries.
/// Carries enough context to retry manually or alert the subscriber.
/// </summary>
public sealed record WebhookFailedEvent(
    Guid DeliveryId,
    string SubscriptionName,
    string TargetUrl,
    string EventType,
    string LastError,
    int Attempts,
    DateTime FailedAt);
