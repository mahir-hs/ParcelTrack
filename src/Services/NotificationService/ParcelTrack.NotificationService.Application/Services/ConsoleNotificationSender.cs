using Microsoft.Extensions.Logging;
using ParcelTrack.NotificationService.Application.Domain;
using ParcelTrack.NotificationService.Application.Interfaces;

namespace ParcelTrack.NotificationService.Application.Services;

/// <summary>
/// Dev sender: logs the notification instead of hitting a real mail provider.
/// Throws ~10% of the time to exercise the retry / dead-letter path (deterministic
/// by attempt parity so it's observable but not flaky in tests).
/// </summary>
public sealed class ConsoleNotificationSender(ILogger<ConsoleNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[NOTIFY:{Channel}] -> {Recipient} | {Subject} | (attempt {Attempt})",
            notification.Channel, notification.Recipient, notification.Subject, notification.Attempts + 1);

        if (notification.Attempts % 2 == 1)
            throw new InvalidOperationException("Simulated transient mail-provider failure.");

        return Task.CompletedTask;
    }
}
