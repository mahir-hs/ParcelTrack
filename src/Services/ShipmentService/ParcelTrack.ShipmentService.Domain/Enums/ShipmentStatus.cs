namespace ParcelTrack.ShipmentService.Domain.Enums;

public enum ShipmentStatus
{
    Created = 0,
    InTransit = 1,
    OutForDelivery = 2,
    Failed = 3,       // delivery attempt failed — not terminal, retry is possible
    Delivered = 4,    // terminal — no further transitions allowed
    Cancelled = 5     // terminal — no further transitions allowed
}
