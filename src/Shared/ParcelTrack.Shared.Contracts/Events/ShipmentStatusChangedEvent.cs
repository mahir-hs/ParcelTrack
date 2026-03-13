namespace ParcelTrack.Shared.Contracts.Events;

/// <summary>
/// Published to Kafka topic 'shipment.status.changed' on every status transition.
/// Consumed by Notification Service (email + WebSocket) and
/// Webhook Dispatch Service (B2B outbound callbacks).
/// </summary>
public sealed record ShipmentStatusChangedEvent(
    Guid ShipmentId,
    string TrackingNumber,
    Guid TenantId,
    Guid UserId,
    string PreviousStatus,
    string NewStatus,
    string? Location,
    string Description,
    DateTime OccurredAt);
