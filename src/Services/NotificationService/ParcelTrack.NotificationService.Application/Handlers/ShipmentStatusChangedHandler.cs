using Microsoft.Extensions.Logging;
using ParcelTrack.NotificationService.Application.DTOs;
using ParcelTrack.NotificationService.Application.Interfaces;
using ParcelTrack.Shared.Contracts.Events;

namespace ParcelTrack.NotificationService.Application.Handlers;

public sealed class ShipmentStatusChangedHandler(
    INotificationSender sender,
    ILogger<ShipmentStatusChangedHandler> logger)
{
    private static readonly HashSet<string> NotifiableStatuses =
    [
        "InTransit",
        "OutForDelivery",
        "Delivered",
        "Failed",
        "Cancelled"
    ];

    public async Task HandleAsync(ShipmentStatusChangedEvent @event, CancellationToken cancellationToken = default)
    {
        if (!NotifiableStatuses.Contains(@event.NewStatus))
        {
            logger.LogDebug(
                "Skipping notification for {TrackingNumber} — status {Status} is not notifiable",
                @event.TrackingNumber, @event.NewStatus);
            return;
        }

        await sender.SendAsync(new NotificationDto(
            TrackingNumber: @event.TrackingNumber,
            NotificationType: "StatusChanged",
            BuyerEmail: null,  // ShipmentStatusChangedEvent does not carry buyer email
            PreviousStatus: @event.PreviousStatus,
            NewStatus: @event.NewStatus),
            cancellationToken);

        logger.LogInformation(
            "Sent StatusChanged notification for {TrackingNumber}: {Previous} → {New}",
            @event.TrackingNumber, @event.PreviousStatus, @event.NewStatus);
    }
}
