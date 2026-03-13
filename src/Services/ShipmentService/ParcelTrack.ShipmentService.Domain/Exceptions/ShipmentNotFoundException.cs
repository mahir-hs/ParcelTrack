namespace ParcelTrack.ShipmentService.Domain.Exceptions;

public sealed class ShipmentNotFoundException : DomainException
{
    public ShipmentNotFoundException(Guid shipmentId)
        : base($"Shipment with ID '{shipmentId}' was not found.", "SHIPMENT_NOT_FOUND")
    {
    }

    public ShipmentNotFoundException(string trackingNumber)
        : base($"Shipment with tracking number '{trackingNumber}' was not found.", "SHIPMENT_NOT_FOUND")
    {
    }
}