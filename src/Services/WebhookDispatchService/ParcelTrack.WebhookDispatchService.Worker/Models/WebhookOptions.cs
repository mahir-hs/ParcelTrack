namespace ParcelTrack.WebhookDispatchService.Worker.Models;

/// <summary>
/// Webhook fan-out configuration, bound from the "Webhooks" section.
/// Each subscription receives a POST for every matching event topic.
/// </summary>
public sealed class WebhookOptions
{
    public const string SectionName = "Webhooks";

    /// <summary>Maximum delivery attempts per subscription before dead-lettering. Defaults to 3.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Base (in seconds) for the exponential-backoff delay between retries: 2^attempt * base. Defaults to 2.</summary>
    public int RetryDelayBaseSeconds { get; set; } = 2;

    public List<Subscription> Subscriptions { get; set; } = [];

    public sealed class Subscription
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        /// <summary>Kafka topic names to forward, e.g. "shipment.created". "*" matches all.</summary>
        public List<string> Events { get; set; } = [];
    }
}
