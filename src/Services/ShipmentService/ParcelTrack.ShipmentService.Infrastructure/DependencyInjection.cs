using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Infrastructure.BackgroundServices;
using ParcelTrack.ShipmentService.Infrastructure.Interfaces;
using ParcelTrack.ShipmentService.Infrastructure.Messaging;
using ParcelTrack.ShipmentService.Infrastructure.Persistence;
using ParcelTrack.ShipmentService.Infrastructure.Persistence.Repositories;

namespace ParcelTrack.ShipmentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ─────────────────────────────────────────────────────────────
        services.AddDbContext<ShipmentDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("ShipmentDb")
                    ?? throw new InvalidOperationException("ConnectionStrings:ShipmentDb is not configured"),
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(ShipmentDbContext).Assembly.FullName);

                    // Retry on transient PostgreSQL failures (connection refused, timeout)
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
        });

        // IUnitOfWork resolves to the same ShipmentDbContext scoped instance
        // Handler + Repository + OutboxEventProducer all share one DbContext per request
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ShipmentDbContext>());

        // ── Repositories ─────────────────────────────────────────────────────────
        services.AddScoped<IShipmentRepository, ShipmentRepository>();

        // ── Messaging ────────────────────────────────────────────────────────────

        // IEventProducer → OutboxEventProducer (writes to DB, not Kafka directly)
        // Handlers call this — they are unaware of the outbox indirection
        services.AddScoped<IEventProducer, OutboxEventProducer>();

        // IKafkaProducer → KafkaProducer (real Kafka connection)
        // Used only by OutboxProcessor — never injected into handlers
        services.AddSingleton<IKafkaProducer, KafkaProducer>();

        // ── Background Services ───────────────────────────────────────────────────
        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}