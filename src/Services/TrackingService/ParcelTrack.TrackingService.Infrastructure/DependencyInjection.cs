using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Infrastructure.Extensions;
using ParcelTrack.TrackingService.Infrastructure.Persistence;
using ParcelTrack.TrackingService.Infrastructure.Persistence.Repositories;

namespace ParcelTrack.TrackingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<TrackingDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("TrackingDb")
                    ?? throw new InvalidOperationException("ConnectionStrings:TrackingDb is not configured"))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<ITrackingRepository, TrackingRepository>();

        services.AddCarriers(configuration);

        services.AddHealthChecks()
            .AddDbContextCheck<TrackingDbContext>("database");

        return services;
    }
}
