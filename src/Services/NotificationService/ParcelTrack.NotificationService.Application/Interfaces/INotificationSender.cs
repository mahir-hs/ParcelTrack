using ParcelTrack.NotificationService.Application.Domain;

namespace ParcelTrack.NotificationService.Application.Interfaces;

/// <summary>
/// Delivers a notification over its channel. The default implementation is a console
/// sender (logs the message) — swap for SMTP / SendGrid / push without touching handlers.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(Notification notification, CancellationToken cancellationToken = default);
}
