using ParcelTrack.Shared.Contracts.Events;

namespace ParcelTrack.TrackingService.Application.Interfaces;

/// <summary>
/// Publishes status changes the couriers told us about.
///
/// TrackingService is normally a consumer; this is the one place it produces. A courier is
/// the only source that knows a parcel actually moved, so an observation has to re-enter the
/// event stream for the notification and webhook services to act on it.
/// </summary>
public interface ICarrierEventPublisher
{
    Task PublishStatusChangedAsync(
        ShipmentStatusChangedEvent @event,
        CancellationToken cancellationToken = default);
}
