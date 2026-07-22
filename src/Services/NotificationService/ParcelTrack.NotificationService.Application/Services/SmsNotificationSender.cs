using Microsoft.Extensions.Logging;
using ParcelTrack.NotificationService.Application.Domain;
using ParcelTrack.NotificationService.Application.Interfaces;

namespace ParcelTrack.NotificationService.Application.Services;

/// <summary>
/// SMS channel sender. Free placeholder: logs the SMS instead of calling a provider,
/// so the SMS path is real and exercisable locally without credentials. Swap for
/// Twilio / a local SMS gateway without touching the handler.
/// </summary>
public sealed class SmsNotificationSender(ILogger<SmsNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[NOTIFY:Sms] -> {Recipient} | {Subject}",
            notification.Recipient, notification.Subject);

        // No provider configured. Kept best-effort (does not throw) so a missing phone
        // number never blocks email delivery on other channels.
        return Task.CompletedTask;
    }
}
