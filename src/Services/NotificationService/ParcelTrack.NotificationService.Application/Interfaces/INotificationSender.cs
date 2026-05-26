using ParcelTrack.NotificationService.Application.DTOs;

namespace ParcelTrack.NotificationService.Application.Interfaces;

public interface INotificationSender
{
    Task SendAsync(NotificationDto notification, CancellationToken cancellationToken = default);
}
