using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.Application.Handlers;

/// <summary>
/// Applies a status transition to the TrackingRecord. If no record exists yet (event
/// arrived before the create event), one is created on demand.
/// </summary>
public sealed class ShipmentStatusChangedEventHandler(ITrackingRepository repository)
{
    private readonly ITrackingRepository _repository = repository;

    public async Task Handle(ShipmentStatusChangedEvent e, CancellationToken cancellationToken = default)
    {
        var record = await _repository.GetByShipmentIdAsync(e.ShipmentId, cancellationToken);

        if (record is null)
        {
            record = TrackingRecord.Create(e.ShipmentId, e.TrackingNumber, e.TenantId, "Unknown");
            await _repository.AddAsync(record, cancellationToken);
        }

        record.ApplyStatusChange(e.NewStatus, e.Location, e.Description, e.OccurredAt);

        await _repository.SaveChangesAsync(cancellationToken);
    }
}
