namespace ParcelTrack.ShipmentService.Domain.Exceptions;

public sealed class MaxDeliveryAttemptsExceededException : DomainException
{
    public Guid ShipmentId { get; }
    public int MaxAttempts { get; }
    public int CurrentAttempts { get; }

    public MaxDeliveryAttemptsExceededException(Guid shipmentId, int currentAttempts, int maxAttempts)
        : base(
            $"Shipment '{shipmentId}' has reached the maximum of {maxAttempts} delivery attempts ({currentAttempts} recorded). No further retry is permitted.",
            "MAX_DELIVERY_ATTEMPTS_EXCEEDED")
    {
        ShipmentId = shipmentId;
        MaxAttempts = maxAttempts;
        CurrentAttempts = currentAttempts;
    }
}
