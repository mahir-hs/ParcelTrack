using Microsoft.Extensions.Logging;
using ParcelTrack.NotificationService.Application.DTOs;
using ParcelTrack.NotificationService.Application.Interfaces;

namespace ParcelTrack.NotificationService.Worker.Notifications;

/// <summary>
/// Logs notifications to stdout. Swap for SMTP / SendGrid before production.
/// </summary>
public sealed class LogNotificationSender(ILogger<LogNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(NotificationDto notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[NOTIFICATION] Type={Type} | Tracking={TrackingNumber} | Buyer={Email} | Status={Previous}→{New}",
            notification.NotificationType,
            notification.TrackingNumber,
            notification.BuyerEmail ?? "n/a",
            notification.PreviousStatus ?? "-",
            notification.NewStatus);

        return Task.CompletedTask;
    }
}
