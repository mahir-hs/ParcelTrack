using Microsoft.AspNetCore.Authentication;
using ParcelTrack.ShipmentService.API;
using ParcelTrack.ShipmentService.API.Extensions;
using ParcelTrack.ShipmentService.API.Infrastructure;
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

        // Auth: Dev mode (header-based identity) is the default in Development so the API
        // runs without Keycloak. In any other environment real Keycloak JWT is enforced.
        // Auth:DevMode appsetting can override (e.g. force false in Production).
        var devMode = configuration.GetValue<bool?>("Auth:DevMode") ?? environment.IsDevelopment();
        if (devMode)
        {
            services.AddAuthentication(DevAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(
                    DevAuthHandler.SchemeName, _ => { });
        }
        else
        {
            services.AddKeycloakAuthentication(configuration, environment);
        }

        services.AddApiDocumentation();

        return services;
    }
}
