using ParcelTrack.TrackingService.Domain.Entities;
using ParcelTrack.TrackingService.Domain.Enums;

namespace ParcelTrack.TrackingService.Application.Interfaces;

public interface ITrackedShipmentRepository
{
    /// <summary>
    /// Active shipments for one courier, oldest-polled first, capped at <paramref name="limit"/>.
    ///
    /// Ordering by last-polled makes the poll cycle fair: with more parcels than one cycle can
    /// cover, every parcel still gets its turn instead of the first N being polled forever.
    /// </summary>
    Task<IReadOnlyList<TrackedShipment>> GetActiveByCarrierAsync(
        CarrierType carrier,
        int limit,
        CancellationToken cancellationToken = default);

    Task<TrackedShipment?> GetByTrackingNumberAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default);

    Task AddAsync(TrackedShipment shipment, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
