namespace ParcelTrack.WebhookDispatchService.Worker.Settings;

public sealed class KafkaSettings
{
    public string BootstrapServers { get; init; } = "localhost:9092";
}
