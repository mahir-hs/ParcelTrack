using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Infrastructure.Persistence;
using ParcelTrack.TrackingService.Infrastructure.Persistence.Repositories;

namespace ParcelTrack.TrackingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTrackingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<TrackingDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("TrackingDb")
                    ?? throw new InvalidOperationException("ConnectionStrings:TrackingDb is not configured"),
                npgsql => npgsql.MigrationsAssembly(typeof(TrackingDbContext).Assembly.FullName)));

        services.AddScoped<ITrackingRepository, TrackingRepository>();

        return services;
    }
}
