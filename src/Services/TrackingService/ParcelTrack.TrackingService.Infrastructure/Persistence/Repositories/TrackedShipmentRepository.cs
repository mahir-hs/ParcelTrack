using Microsoft.EntityFrameworkCore;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;
using ParcelTrack.TrackingService.Domain.Enums;

namespace ParcelTrack.TrackingService.Infrastructure.Persistence.Repositories;

public sealed class TrackedShipmentRepository(TrackingDbContext context) : ITrackedShipmentRepository
{
    public async Task<IReadOnlyList<TrackedShipment>> GetActiveByCarrierAsync(
        CarrierType carrier,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Tracked, not AsNoTracking: the caller mutates these and saves through the same context.
        return await context.TrackedShipments
            .Where(s => s.IsActive && s.CarrierType == carrier)
            .OrderBy(s => s.LastPolledAt ?? DateTime.MinValue)  // never-polled parcels first
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<TrackedShipment?> GetByTrackingNumberAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        return await context.TrackedShipments
            .FirstOrDefaultAsync(s => s.TrackingNumber == trackingNumber, cancellationToken);
    }

    public async Task AddAsync(TrackedShipment shipment, CancellationToken cancellationToken = default)
    {
        await context.TrackedShipments.AddAsync(shipment, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
