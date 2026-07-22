using Microsoft.EntityFrameworkCore;
using ParcelTrack.NotificationService.Application.Domain;
using ParcelTrack.NotificationService.Application.Interfaces;
using ParcelTrack.NotificationService.Application.Persistence;

namespace ParcelTrack.NotificationService.Application.Persistence.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _context;

    public NotificationRepository(NotificationDbContext context) => _context = context;

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default) =>
        await _context.Notifications.AddAsync(notification, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}
