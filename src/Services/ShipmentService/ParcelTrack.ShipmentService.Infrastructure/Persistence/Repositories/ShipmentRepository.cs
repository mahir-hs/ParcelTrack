using Microsoft.EntityFrameworkCore;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.Infrastructure.Persistence.Repositories;

public sealed class ShipmentRepository : IShipmentRepository
{
    private readonly ShipmentDbContext _context;

    public ShipmentRepository(ShipmentDbContext context)
    {
        _context = context;
    }

    public async Task<Shipment?> GetByIdAsyncWithEvents(Guid id, CancellationToken cancellationToken = default)
    {
        // Include Events — callers always need the tracking history alongside the aggregate
        return await _context.Shipments
            .Include(s => s.Events)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        // Note: TenantId filter is applied automatically by the global query filter
        // No need to add .Where(s => s.TenantId == ...) here
    }

    public async Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Include Events — callers always need the tracking history alongside the aggregate
        var result = await _context.Shipments
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        Console.WriteLine(_context?.Entry(result).State);
        return result;
        // Note: TenantId filter is applied automatically by the global query filter
        // No need to add .Where(s => s.TenantId == ...) here
    }

    public async Task<Shipment?> GetByTrackingNumberAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.Shipments
            .Include(s => s.Events)
            .FirstOrDefaultAsync(s => s.TrackingNumber == trackingNumber, cancellationToken);
    }

    /// <summary>
    /// Public tracking lookup — bypasses the global tenant filter intentionally.
    /// Anyone with a tracking number can check their parcel status, regardless of tenant.
    /// </summary>
    public async Task<Shipment?> GetByTrackingNumberPublicAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.Shipments
            .IgnoreQueryFilters()              // Bypass tenant filter — this is the public endpoint
            .Include(s => s.Events.OrderBy(e => e.OccurredAt))
            .AsNoTracking()                    // Read-only — no change tracking needed
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.TrackingNumber == trackingNumber, cancellationToken);
    }

    public async Task AddAsync(Shipment shipment, CancellationToken cancellationToken = default)
    {
        // EF Core tracks the entity — no I/O yet
        // Actual INSERT happens when IUnitOfWork.SaveChangesAsync() is called
        await _context.Shipments.AddAsync(shipment, cancellationToken);
    }

    public async Task<(IReadOnlyList<Shipment> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        ShipmentStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Shipments.AsNoTracking();

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        // COUNT and data fetch in parallel — one round trip each, not sequential
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(s => s.Events)
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}