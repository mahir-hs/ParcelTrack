using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using ParcelTrack.NotificationService.Application.DTOs;
using ParcelTrack.NotificationService.Application.Interfaces;
using ParcelTrack.NotificationService.Worker.Settings;

namespace ParcelTrack.NotificationService.Worker.Notifications;

public sealed class SmtpNotificationSender(
    IOptions<SmtpSettings> options,
    ILogger<SmtpNotificationSender> logger) : INotificationSender
{
    private readonly SmtpSettings _settings = options.Value;

    public async Task SendAsync(NotificationDto notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.BuyerEmail))
        {
            logger.LogInformation(
                "Skipping email for {TrackingNumber} — no buyer email",
                notification.TrackingNumber);
            return;
        }

        var (subject, body) = notification.NotificationType switch
        {
            "ShipmentCreated" => EmailTemplates.ShipmentCreated(notification.TrackingNumber),
            "StatusChanged"   => EmailTemplates.StatusChanged(
                                     notification.TrackingNumber,
                                     notification.PreviousStatus ?? string.Empty,
                                     notification.NewStatus),
            _ => throw new InvalidOperationException(
                     $"Unknown notification type: {notification.NotificationType}")
        };

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(notification.BuyerEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation(
            "Sent {Type} email for {TrackingNumber} to {Email}",
            notification.NotificationType, notification.TrackingNumber, notification.BuyerEmail);
    }
}
