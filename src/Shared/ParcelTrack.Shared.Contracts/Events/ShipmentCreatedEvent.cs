namespace ParcelTrack.Shared.Contracts.Events;

/// <summary>
/// Published to Kafka topic 'shipment.created' when a new shipment is registered.
/// Consumed by the Tracking Service to start polling/watching the carrier.
/// </summary>
public sealed record ShipmentCreatedEvent(
    Guid ShipmentId,
    string TrackingNumber,
    string CarrierType,
    Guid UserId,
    Guid TenantId,
    string? BuyerEmail,
    DateTime CreatedAt);
