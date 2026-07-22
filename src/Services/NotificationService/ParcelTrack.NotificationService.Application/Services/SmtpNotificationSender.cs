using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using ParcelTrack.NotificationService.Application.Domain;
using ParcelTrack.NotificationService.Application.Interfaces;

namespace ParcelTrack.NotificationService.Application.Services;

/// <summary>
/// Real SMTP sender. Free when pointed at a local Mailpit (or any SMTP server).
/// Delivers the notification email and throws on failure so the existing retry /
/// dead-letter path in ShipmentStatusChangedEventHandler engages.
/// </summary>
public sealed class SmtpNotificationSender(
    IConfiguration configuration,
    ILogger<SmtpNotificationSender> logger)
    : INotificationSender
{
    public async Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        var host = configuration["Notification:Smtp:Host"]
            ?? throw new InvalidOperationException("Notification:Smtp:Host is not configured.");
        var port = int.TryParse(configuration["Notification:Smtp:Port"], out var p) ? p : 25;
        var from = configuration["Notification:Smtp:From"] ?? "no-reply@parceltrack.dev";
        var user = configuration["Notification:Smtp:User"];
        var pass = configuration["Notification:Smtp:Password"];

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(notification.Recipient));
        message.Subject = notification.Subject;
        message.Body = new TextPart("plain") { Text = notification.Body };

        using var client = new SmtpClient();
        // Local dev (Mailpit) is plain SMTP on 1025 — no TLS.
        await client.ConnectAsync(host, port, SecureSocketOptions.None, cancellationToken);
        if (!string.IsNullOrEmpty(user))
            await client.AuthenticateAsync(user, pass ?? string.Empty, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation(
            "[NOTIFY:Email] -> {Recipient} | {Subject}",
            notification.Recipient, notification.Subject);
    }
}
