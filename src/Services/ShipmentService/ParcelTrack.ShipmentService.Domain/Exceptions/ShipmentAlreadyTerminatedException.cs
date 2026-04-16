using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.Domain.Exceptions;

/// <summary>
/// Thrown when an operation (status update, cancellation) is attempted on a shipment
/// that has already reached a terminal state: Delivered or Cancelled.
///
/// Terminal states are final — no further transitions are valid. This exception
/// exists separately from InvalidShipmentStatusTransitionException because "already
/// terminated" has a distinct meaning in the domain: the shipment's lifecycle is over,
/// regardless of what transition was attempted.
///
/// Example: Attempting to mark a Delivered shipment as OutForDelivery throws this,
/// not a generic transition error.
/// </summary>
public sealed class ShipmentAlreadyTerminatedException : DomainException
{
    public ShipmentStatus CurrentStatus { get; }

    public ShipmentAlreadyTerminatedException(Guid shipmentId, ShipmentStatus currentStatus)
        : base(
            $"Shipment '{shipmentId}' is already in terminal state '{currentStatus}' and cannot be modified.",
            "SHIPMENT_ALREADY_TERMINATED")
    {
        CurrentStatus = currentStatus;
    }
}