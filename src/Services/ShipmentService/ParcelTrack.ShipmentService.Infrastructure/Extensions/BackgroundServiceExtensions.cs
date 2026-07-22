using Microsoft.Extensions.DependencyInjection;
using ParcelTrack.ShipmentService.Infrastructure.BackgroundServices;

namespace ParcelTrack.ShipmentService.Infrastructure.Extensions;

public static class BackgroundServiceExtensions
{
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        // Polls outbox_messages and publishes pending events to Kafka.
        // Resilient: a broker outage only leaves messages pending — it never crashes.
        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}