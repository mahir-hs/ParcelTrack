using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParcelTrack.NotificationService.Application;
using ParcelTrack.NotificationService.Application.Domain;
using ParcelTrack.NotificationService.Application.Interfaces;
using ParcelTrack.Shared.Contracts;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.Shared.Messaging;

namespace ParcelTrack.NotificationService.Application.Handlers;

/// <summary>
/// On customer-visible status transitions, persists a Notification per configured channel
/// and attempts delivery. Failures are retried across event redeliveries; once attempts are
/// exhausted the notification is marked Failed and a notification.failed event is published.
/// </summary>
public sealed class ShipmentStatusChangedEventHandler(
    INotificationRepository repository,
    INotificationSender sender,
    IKafkaProducer kafkaProducer,
    IOptions<NotificationOptions> options,
    ILogger<ShipmentStatusChangedEventHandler> logger)
{
    private static readonly HashSet<string> NotifyOn = new(StringComparer.Ordinal)
    {
        "Created", "OutForDelivery", "Delivered", "Failed", "Cancelled"
    };

    public async Task Handle(ShipmentStatusChangedEvent e, CancellationToken cancellationToken = default)
    {
        if (!NotifyOn.Contains(e.NewStatus))
            return;

        foreach (var channel in options.Value.Channels)
        {
            var notification = Notification.Create(
                e.ShipmentId, e.TenantId, e.UserId, e.NewStatus, e.TrackingNumber,
                e.BuyerEmail, e.BuyerPhone, channel);

            await repository.AddAsync(notification, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);

            try
            {
                await sender.SendAsync(notification, cancellationToken);
                notification.MarkSent();
                logger.LogInformation(
                    "Notification {Id} sent ({Channel}) for shipment {ShipmentId}",
                    notification.Id, channel, e.ShipmentId);
            }
            catch (Exception ex)
            {
                notification.RecordFailure(ex.Message);
                logger.LogWarning(ex,
                    "Notification {Id} delivery failed ({Channel}, attempt {Attempt})",
                    notification.Id, channel, notification.Attempts);

                if (notification.ShouldDeadLetter)
                {
                    var failed = new NotificationFailedEvent(
                        notification.Id, e.ShipmentId, e.TenantId, notification.Channel,
                        notification.Recipient, ex.Message, DateTime.UtcNow);

                    await kafkaProducer.ProduceAsync(
                        Topics.NotificationFailed,
                        typeof(NotificationFailedEvent).FullName!,
                        JsonSerializer.Serialize(failed),
                        cancellationToken);
                }
            }

            await repository.SaveChangesAsync(cancellationToken);
        }
    }
}
