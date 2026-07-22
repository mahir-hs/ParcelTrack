namespace ParcelTrack.Shared.Contracts.Events;

/// <summary>
/// Published to 'notification.failed' when a notification cannot be delivered after
/// the configured number of retries. Consumed by an ops/alerting sink (or just logged).
/// </summary>
public sealed record NotificationFailedEvent(
    Guid NotificationId,
    Guid ShipmentId,
    Guid TenantId,
    string Channel,
    string Recipient,
    string LastError,
    DateTime FailedAt);
