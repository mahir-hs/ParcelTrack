using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Infrastructure.Persistence;
using ParcelTrack.ShipmentService.Infrastructure.Persistence.Repositories;

namespace ParcelTrack.ShipmentService.Infrastructure.Extensions;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ShipmentDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("ShipmentDb")
                    ?? throw new InvalidOperationException(
                        "ConnectionStrings:ShipmentDb is not configured"),
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(ShipmentDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
        });

        // IUnitOfWork resolves to the same scoped ShipmentDbContext instance
        // Handler + Repository + OutboxEventProducer all share one DbContext per request
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ShipmentDbContext>());
        services.AddScoped<IShipmentRepository, ShipmentRepository>();

        return services;
    }
}