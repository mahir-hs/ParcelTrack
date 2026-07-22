namespace ParcelTrack.TrackingService.Domain.Entities;

/// <summary>
/// Read-model projection of a shipment's lifecycle, built by consuming
/// shipment.created / shipment.status.changed events. Optimized for fast
/// "where is my parcel" lookups by the tracking UI.
/// </summary>
public sealed class TrackingRecord
{
    public Guid ShipmentId { get; private set; }
    public string TrackingNumber { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public string CarrierType { get; private set; } = string.Empty;
    public string CurrentStatus { get; private set; } = string.Empty;
    public string? LastLocation { get; private set; }
    public DateTime? LastEventAt { get; private set; }

    private readonly List<TrackingEvent> _events = [];
    public IReadOnlyCollection<TrackingEvent> Events => _events.AsReadOnly();

    private TrackingRecord() { }

    public static TrackingRecord Create(
        Guid shipmentId,
        string trackingNumber,
        Guid tenantId,
        string carrierType)
    {
        var record = new TrackingRecord
        {
            ShipmentId = shipmentId,
            TrackingNumber = trackingNumber,
            TenantId = tenantId,
            CarrierType = carrierType,
            CurrentStatus = "Created",
            LastEventAt = DateTime.UtcNow
        };

        record._events.Add(new TrackingEvent(
            shipmentId, "Created", "Shipment registered for tracking.", null, DateTime.UtcNow));

        return record;
    }

    public void ApplyStatusChange(string newStatus, string? location, string description, DateTime occurredAt)
    {
        CurrentStatus = newStatus;
        LastLocation = location;
        if (occurredAt > LastEventAt || LastEventAt is null)
            LastEventAt = occurredAt;

        _events.Add(new TrackingEvent(ShipmentId, newStatus, description, location, occurredAt));
    }
}

public sealed class TrackingEvent
{
    public Guid Id { get; private set; }
    public Guid ShipmentId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; }

    private TrackingEvent() { }

    public TrackingEvent(Guid shipmentId, string status, string description, string? location, DateTime occurredAt)
    {
        ShipmentId = shipmentId;
        Status = status;
        Description = description;
        Location = location;
        OccurredAt = occurredAt;
    }
}
