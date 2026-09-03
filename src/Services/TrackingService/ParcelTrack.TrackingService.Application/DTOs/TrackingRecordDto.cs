namespace ParcelTrack.TrackingService.Application.DTOs;

public sealed record TrackingRecordDto(
    Guid ShipmentId,
    string TrackingNumber,
    string Status,
    string? Location,
    string Description,
    string CarrierType,
    DateTime OccurredAt);
