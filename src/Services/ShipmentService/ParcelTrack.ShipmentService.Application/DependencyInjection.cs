using Microsoft.Extensions.DependencyInjection;
using ParcelTrack.ShipmentService.Application.Handler;
using ParcelTrack.ShipmentService.Application.Mappings;

namespace ParcelTrack.ShipmentService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        MappingConfig.Configure();
        // Command handlers
        services.AddScoped<CreateShipmentCommandHandler>();
        services.AddScoped<UpdateShipmentStatusCommandHandler>();
        services.AddScoped<CancelShipmentCommandHandler>();

        // Query handlers
        services.AddScoped<GetShipmentByIdQueryHandler>();
        services.AddScoped<GetShipmentsQueryHandler>();
        services.AddScoped<GetShipmentByTrackingNumberQueryHandler>();

        return services;
    }
}
