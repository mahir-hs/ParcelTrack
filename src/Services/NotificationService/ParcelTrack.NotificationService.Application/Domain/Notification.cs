namespace ParcelTrack.NotificationService.Application.Domain;

/// <summary>
/// A notification queued and sent when a shipment reaches a customer-visible state.
/// Persisted so delivery is auditable and retriable.
/// </summary>
public sealed class Notification
{
    private const int MaxAttempts = 3;

    public Guid Id { get; private set; }
    public Guid ShipmentId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Channel { get; private set; } = "Email";
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string Status { get; private set; } = "Pending"; // Pending | Sent | Failed
    public int Attempts { get; private set; }
    public string? Error { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid shipmentId,
        Guid tenantId,
        Guid userId,
        string status,
        string trackingNumber,
        string? buyerEmail,
        string? buyerPhone = null,
        string channel = "Email")
    {
        var recipient = string.Equals(channel, "Sms", StringComparison.OrdinalIgnoreCase)
            ? (buyerPhone ?? "ops@parceltrack.dev")
            : (buyerEmail ?? "ops@parceltrack.dev");

        return new Notification
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            TenantId = tenantId,
            UserId = userId,
            Channel = channel,
            Recipient = recipient,
            Subject = $"Your parcel {trackingNumber} is now {status}",
            Body = $"Hi, shipment {trackingNumber} status changed to '{status}'.",
            Status = "Pending",
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkSent()
    {
        Status = "Sent";
        SentAt = DateTime.UtcNow;
        Error = null;
    }

    public void RecordFailure(string error)
    {
        Attempts++;
        Error = error;
        if (Attempts >= MaxAttempts)
            Status = "Failed";
    }

    public bool ShouldDeadLetter => Status == "Failed";
}
