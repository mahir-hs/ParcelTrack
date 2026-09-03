namespace ParcelTrack.TrackingService.Worker.Settings;

public sealed class CarrierWebhookSettings
{
    public const string SectionName = "CarrierWebhooks";

    /// <summary>
    /// Header the courier echoes back to prove the call is theirs.
    /// Verify the exact name against Pathao's merchant webhook documentation before going live.
    /// </summary>
    public const string SecretHeaderName = "X-Pathao-Signature";

    /// <summary>Shared secret agreed with Pathao when registering the webhook. Empty disables the check.</summary>
    public string? PathaoSecret { get; set; }
}
