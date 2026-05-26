using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.Application.Interfaces;

public interface ITrackingRepository
{
    Task AddAsync(TrackingRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackingRecord>> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default);
}
