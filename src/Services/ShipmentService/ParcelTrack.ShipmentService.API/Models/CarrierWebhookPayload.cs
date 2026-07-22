namespace ParcelTrack.ShipmentService.API.Models;

/// <summary>
/// Generic inbound carrier status push. Real Pathao/Steadfast/RedX adapters would
/// implement a per-carrier normalizer that maps their bespoke payloads onto this shape.
/// </summary>
public sealed record CarrierWebhookPayload(
    string TrackingNumber,
    string Status,
    string? Location,
    string? Description,
    DateTime? Timestamp);
