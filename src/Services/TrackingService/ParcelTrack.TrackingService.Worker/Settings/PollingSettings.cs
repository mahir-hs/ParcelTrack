namespace ParcelTrack.TrackingService.Worker.Settings;

public sealed class PollingSettings
{
    public const string SectionName = "Polling";

    /// <summary>Set false to run consumer-only — useful when carrier credentials are absent.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Seconds between cycles. Courier statuses change on the order of hours, so polling
    /// faster mostly buys rate-limit problems; 30s is the plan's figure and errs toward fresh.
    /// </summary>
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>Parcels polled per carrier per cycle. Bounds one cycle's work and the courier's load.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Delay before the first cycle, so migrations and Kafka are ready first.</summary>
    public int StartupDelaySeconds { get; set; } = 15;
}
