using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ParcelTrack.TrackingService.Infrastructure.Extensions;

public static class MessagingExtensions
{
    /// <summary>
    /// Registers the Kafka producer used to publish courier observations.
    ///
    /// Singleton because a Confluent producer is thread-safe, holds its own connection pool and
    /// batching buffers, and is meant to be created once per process. Creating one per scope
    /// would rebuild TCP connections on every poll cycle.
    /// </summary>
    public static IServiceCollection AddKafkaProducer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

        services.AddSingleton<IProducer<string, string>>(_ =>
        {
            var config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,

                // Wait for all in-sync replicas. A courier observation is not replayable from
                // anywhere else, so durability matters more than the microseconds it costs.
                Acks = Acks.All,
                EnableIdempotence = true,
                MessageSendMaxRetries = 3,
                LingerMs = 5
            };

            return new ProducerBuilder<string, string>(config).Build();
        });

        return services;
    }
}
