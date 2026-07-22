namespace ParcelTrack.NotificationService.Application;

/// <summary>
/// Notification fan-out configuration, bound from the "Notification" section.
/// Controls which channels (Email, Sms, ...) receive a message on each
/// customer-visible shipment status change.
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notification";

    public List<string> Channels { get; set; } = new() { "Email" };
}
