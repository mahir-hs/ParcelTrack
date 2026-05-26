using Microsoft.Extensions.Logging;
using ParcelTrack.NotificationService.Application.DTOs;
using ParcelTrack.NotificationService.Application.Interfaces;
using ParcelTrack.Shared.Contracts.Events;

namespace ParcelTrack.NotificationService.Application.Handlers;

public sealed class ShipmentCreatedHandler(
    INotificationSender sender,
    ILogger<ShipmentCreatedHandler> logger)
{
    public async Task HandleAsync(ShipmentCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(@event.BuyerEmail))
        {
            logger.LogInformation(
                "Skipping notification for shipment {ShipmentId} — no buyer email",
                @event.ShipmentId);
            return;
        }

        await sender.SendAsync(new NotificationDto(
            TrackingNumber: @event.TrackingNumber,
            NotificationType: "ShipmentCreated",
            BuyerEmail: @event.BuyerEmail,
            PreviousStatus: null,
            NewStatus: "Created"),
            cancellationToken);

        logger.LogInformation(
            "Sent ShipmentCreated notification for {TrackingNumber} to {Email}",
            @event.TrackingNumber, @event.BuyerEmail);
    }
}
