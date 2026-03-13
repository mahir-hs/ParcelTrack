using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.Domain.Exceptions;
/// <summary>
/// Thrown when a status transition is attempted that violates the shipment
/// state machine rules — and the shipment is NOT already in a terminal state.
///
/// Valid transitions are:
///   Created         → InTransit
///   InTransit       → OutForDelivery
///   InTransit       → Failed
///   OutForDelivery  → Delivered
///   OutForDelivery  → Failed
///   Failed          → OutForDelivery  (retry — up to 3 attempts)
///
/// Example: Created → Delivered (skipping intermediate states) throws this exception.
/// </summary>
public sealed class InvalidShipmentStatusTransitionException : DomainException
{
    public ShipmentStatus From { get; }
    public ShipmentStatus To { get; }

    public InvalidShipmentStatusTransitionException(Guid shipmentId, ShipmentStatus from, ShipmentStatus to)
        : base(
            $"Shipment '{shipmentId}' cannot transition from '{from}' to '{to}'. This transition is not permitted by the state machine.",
            "INVALID_STATUS_TRANSITION")
    {
        From = from;
        To = to;
    }
}
