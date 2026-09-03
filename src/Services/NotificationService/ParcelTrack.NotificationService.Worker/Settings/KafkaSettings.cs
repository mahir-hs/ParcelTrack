namespace ParcelTrack.NotificationService.Worker.Settings;

public sealed class KafkaSettings
{
    public string BootstrapServers { get; init; } = "localhost:9092";
}
