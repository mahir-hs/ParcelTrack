namespace ParcelTrack.ShipmentService.Domain.Exceptions;

public sealed class DuplicateTrackingNumberException : DomainException
{
    public DuplicateTrackingNumberException(string trackingNumber)
        : base($"A shipment with tracking number '{trackingNumber}' already exists.", "SHIPMENT_NOT_FOUND") { }
}

