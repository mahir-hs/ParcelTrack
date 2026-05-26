using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Infrastructure.Cache;

namespace ParcelTrack.ShipmentService.Infrastructure.Extensions;

public static class CacheExtensions
{
    public static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = connectionString;
                options.InstanceName = "parceltrack:";
            });
        }
        else
        {
            // Fall back to in-memory cache when Redis is not configured (local dev without Docker)
            services.AddDistributedMemoryCache();
        }

        services.AddScoped<IIdempotencyService, RedisIdempotencyService>();

        return services;
    }
}
