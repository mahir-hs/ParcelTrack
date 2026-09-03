using ParcelTrack.Shared.Contracts.Events;

namespace ParcelTrack.TrackingService.Application.Interfaces;

/// <summary>
/// Publishes status changes the couriers told us about.
///
/// TrackingService is normally a consumer; this is the one place it produces. A courier is
/// the only source that knows a parcel actually moved, so an observation has to re-enter the
/// event stream.
///
/// What is published is an observation, not a decision — ShipmentService validates it against
/// the shipment's state machine and republishes the authoritative status change from there.
/// </summary>
public interface ICarrierEventPublisher
{
    Task PublishObservationAsync(
        CarrierStatusObservedEvent @event,
        CancellationToken cancellationToken = default);
}
