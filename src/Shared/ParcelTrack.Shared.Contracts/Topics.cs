namespace ParcelTrack.Shared.Contracts;

/// <summary>
/// Central registry of all Kafka topic names used across services.
/// Every producer and consumer references this class — no magic strings anywhere.
/// </summary>
public static class Topics
{
    public const string ShipmentCreated = "shipment.created";
    public const string ShipmentStatusChanged = "shipment.status.changed";
    public const string NotificationFailed = "notification.failed";
    public const string WebhookFailed = "webhook.failed";
}