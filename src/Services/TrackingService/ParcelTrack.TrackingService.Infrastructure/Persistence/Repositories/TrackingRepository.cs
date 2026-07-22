using Microsoft.EntityFrameworkCore;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;
using ParcelTrack.TrackingService.Infrastructure.Persistence;

namespace ParcelTrack.TrackingService.Infrastructure.Persistence.Repositories;

public sealed class TrackingRepository : ITrackingRepository
{
    private readonly TrackingDbContext _context;

    public TrackingRepository(TrackingDbContext context) => _context = context;

    public async Task<TrackingRecord?> GetByShipmentIdAsync(Guid shipmentId, CancellationToken cancellationToken = default) =>
        await _context.TrackingRecords
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.ShipmentId == shipmentId, cancellationToken);

    public async Task AddAsync(TrackingRecord record, CancellationToken cancellationToken = default) =>
        await _context.TrackingRecords.AddAsync(record, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}
