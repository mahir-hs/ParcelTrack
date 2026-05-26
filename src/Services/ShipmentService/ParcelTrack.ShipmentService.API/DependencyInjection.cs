using ParcelTrack.ShipmentService.API.Infrastructure;
using ParcelTrack.ShipmentService.API.Extensions;
using ParcelTrack.ShipmentService.Application.Interfaces;

namespace ParcelTrack.ShipmentService.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddControllers();
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, TenantContext>();

        services.AddKeycloakAuthentication(configuration, environment);
        services.AddApiDocumentation();

        return services;
    }
}