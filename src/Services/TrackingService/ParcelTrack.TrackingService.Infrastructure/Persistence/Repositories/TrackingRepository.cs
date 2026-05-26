using Microsoft.EntityFrameworkCore;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;
using ParcelTrack.TrackingService.Infrastructure.Persistence;

namespace ParcelTrack.TrackingService.Infrastructure.Persistence.Repositories;

public sealed class TrackingRepository(TrackingDbContext context) : ITrackingRepository
{
    public async Task AddAsync(TrackingRecord record, CancellationToken cancellationToken = default)
    {
        await context.TrackingRecords.AddAsync(record, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrackingRecord>> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        return await context.TrackingRecords
            .AsNoTracking()
            .Where(r => r.TrackingNumber == trackingNumber)
            .OrderBy(r => r.OccurredAt)
            .ToListAsync(cancellationToken);
    }
}
