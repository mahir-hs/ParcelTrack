using ParcelTrack.NotificationService.Application.Domain;
using ParcelTrack.NotificationService.Application.Interfaces;

namespace ParcelTrack.NotificationService.Application.Services;

/// <summary>
/// Routes a notification to the correct underlying sender based on its Channel.
/// Registered as the single INotificationSender the handler depends on, so the
/// handler stays channel-agnostic.
/// </summary>
public sealed class ChannelNotificationSender(
    INotificationSender emailSender,
    INotificationSender smsSender)
    : INotificationSender
{
    public Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        var sender = string.Equals(notification.Channel, "Sms", StringComparison.OrdinalIgnoreCase)
            ? smsSender
            : emailSender;

        return sender.SendAsync(notification, cancellationToken);
    }
}
