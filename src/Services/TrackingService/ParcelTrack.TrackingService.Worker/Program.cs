using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ParcelTrack.TrackingService.Application.Handlers;
using ParcelTrack.TrackingService.Infrastructure;
using ParcelTrack.TrackingService.Infrastructure.Persistence;
using ParcelTrack.TrackingService.Worker;
using ParcelTrack.TrackingService.Worker.Settings;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((_, config) => config
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("parceltrack-tracking"))
    .WithTracing(t => t
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka"));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ShipmentCreatedHandler>();
builder.Services.AddScoped<ShipmentStatusChangedHandler>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();
