namespace ParcelTrack.NotificationService.Application.DTOs;

public sealed record NotificationDto(
    string TrackingNumber,
    string NotificationType,   // "ShipmentCreated" | "StatusChanged"
    string? BuyerEmail,
    string? PreviousStatus,
    string NewStatus);
