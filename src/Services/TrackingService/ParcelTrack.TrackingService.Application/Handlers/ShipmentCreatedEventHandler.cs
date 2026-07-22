using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.Application.Handlers;

/// <summary>
/// Projects a ShipmentCreatedEvent into a new TrackingRecord (idempotent — a duplicate
/// create event is ignored).
/// </summary>
public sealed class ShipmentCreatedEventHandler(ITrackingRepository repository)
{
    private readonly ITrackingRepository _repository = repository;

    public async Task Handle(ShipmentCreatedEvent e, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByShipmentIdAsync(e.ShipmentId, cancellationToken);
        if (existing is not null)
            return;

        await _repository.AddAsync(
            TrackingRecord.Create(e.ShipmentId, e.TrackingNumber, e.TenantId, e.CarrierType),
            cancellationToken);

        await _repository.SaveChangesAsync(cancellationToken);
    }
}
