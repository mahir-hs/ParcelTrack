using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.Domain.Entities;

public sealed class ShipmentEvent
{
    public Guid Id { get; private set; }
    public Guid ShipmentId { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? Location { get; private set; } = null;
    public DateTime OccurredAt { get; private set; }

    // EF Core needs a parameterless constructor — keep it private
    private ShipmentEvent() { }

    public ShipmentEvent(Guid shipmentId, ShipmentStatus status, string description, string location)
    {
        Id = Guid.NewGuid();
        ShipmentId = shipmentId;
        Status = status;
        Description = description;
        Location = location;
        OccurredAt = DateTime.UtcNow;
    }
}
