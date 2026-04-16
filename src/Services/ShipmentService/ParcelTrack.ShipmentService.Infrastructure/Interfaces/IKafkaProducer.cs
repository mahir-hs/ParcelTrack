namespace ParcelTrack.ShipmentService.Infrastructure.Interfaces;

public interface IKafkaProducer
{
    Task ProduceAsync(
        string topic,
        string type,
        string payload,
        CancellationToken cancellationToken = default);
}
