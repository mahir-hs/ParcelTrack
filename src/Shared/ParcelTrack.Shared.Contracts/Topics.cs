namespace ParcelTrack.Shared.Contracts;

/// <summary>
/// Central registry of all Kafka topic names used across services.
/// Every producer and consumer references this class — no magic strings anywhere.
/// </summary>
public static class Topics
{
    public const string ShipmentCreated = "shipment.created";
    public const string ShipmentStatusChanged = "shipment.status.changed";

    /// <summary>
    /// Raw courier observations from TrackingService, consumed by ShipmentService.
    /// Separate from ShipmentStatusChanged so an unvalidated observation is never mistaken
    /// for an applied decision — and so the two do not publish each other in a loop.
    /// </summary>
    public const string CarrierStatusObserved = "carrier.status.observed";
    public const string NotificationFailed = "notification.failed";
    public const string WebhookFailed = "webhook.failed";
}