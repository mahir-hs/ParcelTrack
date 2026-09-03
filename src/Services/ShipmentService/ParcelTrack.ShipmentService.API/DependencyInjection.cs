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
        // One scoped instance behind both interfaces: a background consumer sets the tenant
        // and the handlers in that same scope read it back.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());

        services.AddKeycloakAuthentication(configuration, environment);
        services.AddApiDocumentation();

        return services;
    }
}