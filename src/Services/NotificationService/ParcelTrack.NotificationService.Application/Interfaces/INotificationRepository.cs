using ParcelTrack.NotificationService.Application.Domain;

namespace ParcelTrack.NotificationService.Application.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
