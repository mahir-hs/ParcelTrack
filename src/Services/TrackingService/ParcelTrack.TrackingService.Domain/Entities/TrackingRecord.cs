namespace ParcelTrack.TrackingService.Domain.Entities;

public sealed class TrackingRecord
{
    public Guid Id { get; private set; }
    public Guid ShipmentId { get; private set; }
    public string TrackingNumber { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string CarrierType { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private TrackingRecord() { }

    public static TrackingRecord Create(
        Guid shipmentId,
        string trackingNumber,
        string status,
        string description,
        string carrierType,
        Guid tenantId,
        DateTime occurredAt,
        string? location = null) => new()
    {
        Id = Guid.NewGuid(),
        ShipmentId = shipmentId,
        TrackingNumber = trackingNumber,
        Status = status,
        Description = description,
        CarrierType = carrierType,
        Location = location,
        TenantId = tenantId,
        OccurredAt = occurredAt
    };
}
