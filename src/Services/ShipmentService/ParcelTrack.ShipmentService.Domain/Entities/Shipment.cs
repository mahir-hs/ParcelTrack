using ParcelTrack.ShipmentService.Domain.Enums;
using ParcelTrack.ShipmentService.Domain.Exceptions;

namespace ParcelTrack.ShipmentService.Domain.Entities;

public sealed class Shipment
{
    // ── Configuration ──────────────────────────────────────────
    private const int MaxDeliveryAttempts = 3;

    private static readonly Dictionary<ShipmentStatus, ShipmentStatus[]> AllowedTransitions = new()
    {
        { ShipmentStatus.Created,        [ShipmentStatus.InTransit, ShipmentStatus.Cancelled] },
        { ShipmentStatus.InTransit,      [ShipmentStatus.OutForDelivery, ShipmentStatus.Failed, ShipmentStatus.Cancelled] },
        { ShipmentStatus.OutForDelivery, [ShipmentStatus.Delivered, ShipmentStatus.Failed] },
        { ShipmentStatus.Failed,         [ShipmentStatus.OutForDelivery] },   // retry allowed
        { ShipmentStatus.Delivered,      [] },                                // terminal
        { ShipmentStatus.Cancelled,      [] },                                // terminal
    };

    // ── Properties ─────────────────────────────────────────────
    public Guid Id { get; private set; }
    public string TrackingNumber { get; private set; } = string.Empty;
    public CarrierType CarrierType { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public string? BuyerEmail { get; private set; }  // nullable — B2B tenants may omit
    public int DeliveryAttempts { get; private set; }  // exposed for DTOs and business reporting
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<ShipmentEvent> _events = [];
    public IReadOnlyCollection<ShipmentEvent> Events => _events.AsReadOnly();

    // ── EF Core constructor ─────────────────────────────────────
    private Shipment() { }

    // ── Factory method ──────────────────────────────────────────
    public static Shipment Create(
        string trackingNumber,
        CarrierType carrierType,
        string? buyerEmail,
        Guid userId,
        Guid tenantId)
    {
        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            TrackingNumber = trackingNumber,
            CarrierType = carrierType,
            Status = ShipmentStatus.Created,
            BuyerEmail = buyerEmail,
            DeliveryAttempts = 0,
            UserId = userId,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        shipment._events.Add(new ShipmentEvent(
            shipment.Id,
            ShipmentStatus.Created,
            "Shipment registered in ParcelTrack.",
            string.Empty));

        return shipment;
    }

    // ── Behaviour ───────────────────────────────────────────────

    public void UpdateStatus(ShipmentStatus newStatus, string description, string? location)
    {
        // 1. Block all transitions out of terminal states first
        if (IsTerminal)
            throw new ShipmentAlreadyTerminatedException(Id, Status);

        // 2. Validate transition against state machine
        if (!AllowedTransitions[Status].Contains(newStatus))
            throw new InvalidShipmentStatusTransitionException(Id, Status, newStatus);

        // 3. Enforce max delivery attempt rule on retry
        if (newStatus == ShipmentStatus.OutForDelivery)
        {
            if (DeliveryAttempts >= MaxDeliveryAttempts)
                throw new MaxDeliveryAttemptsExceededException(Id, DeliveryAttempts, MaxDeliveryAttempts);

            DeliveryAttempts++;
        }

        // 4. Record event and update state
        _events.Add(new ShipmentEvent(Id, newStatus, description, location ?? string.Empty));
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(string reason)
    {
        UpdateStatus(ShipmentStatus.Cancelled, reason, string.Empty);
    }

    public bool IsTerminal =>
        Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled;
}