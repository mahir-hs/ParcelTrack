using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.Application.Interfaces;

public interface ITrackingRepository
{
    Task<TrackingRecord?> GetByShipmentIdAsync(Guid shipmentId, CancellationToken cancellationToken = default);
    Task AddAsync(TrackingRecord record, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
