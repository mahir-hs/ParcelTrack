namespace ParcelTrack.TrackingService.Worker.Settings;

public sealed class PollingSettings
{
    public const string SectionName = "Polling";

    /// <summary>
    /// Off by default, and deliberately so: polling without carrier credentials fails every
    /// cycle and trips the circuit breaker for no benefit. Turned on in Development (which has
    /// sandbox credentials) and in any environment where real credentials are configured.
    /// </summary>
    public bool Enabled { get; set; }

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
