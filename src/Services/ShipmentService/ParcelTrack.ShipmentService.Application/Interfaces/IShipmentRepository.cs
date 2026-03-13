using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.Application.Interfaces;

/// <summary>
/// Persistence contract for shipments.
/// Implemented in the Infrastructure layer using EF Core (Week 4).
/// The Application layer only sees this interface — never EF Core directly.
/// </summary>
public interface IShipmentRepository
{
    Task<Shipment?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Shipment?> GetByTrackingNumberAsync(string trackingNumber, Guid tenantId, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Shipment> Items, int TotalCount)> GetPagedAsync(
        Guid tenantId,
        int page,
        int pageSize,
        ShipmentStatus? statusFilter,
        CancellationToken cancellationToken = default);
    Task AddAsync(Shipment shipment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Shipment shipment, CancellationToken cancellationToken = default);
}
