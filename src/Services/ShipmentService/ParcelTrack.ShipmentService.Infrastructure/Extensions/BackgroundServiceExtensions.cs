using Microsoft.Extensions.DependencyInjection;
using ParcelTrack.ShipmentService.Infrastructure.BackgroundServices;

namespace ParcelTrack.ShipmentService.Infrastructure.Extensions;

public static class BackgroundServiceExtensions
{
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        //services.AddHostedService<OutboxProcessor>();

        return services;
    }
}