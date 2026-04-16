using Microsoft.Extensions.DependencyInjection;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Infrastructure.Interfaces;
using ParcelTrack.ShipmentService.Infrastructure.Messaging;

namespace ParcelTrack.ShipmentService.Infrastructure.Extensions;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        // IEventProducer → OutboxEventProducer (writes to outbox table, not Kafka directly)
        // Handlers call this — they are unaware of the outbox indirection
        services.AddScoped<IEventProducer, OutboxEventProducer>();

        // IKafkaProducer → KafkaProducer (real Kafka connection)
        // Singleton: one producer instance reused across all requests (Confluent.Kafka best practice)
        // Used only by OutboxProcessor — never injected into handlers
        //services.AddSingleton<IKafkaProducer, KafkaProducer>();

        return services;
    }
}