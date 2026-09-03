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

// A web host rather than a plain worker host: this service now also receives status pushes
// from couriers, so it needs to listen as well as consume and poll.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((_, config) => config
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("parceltrack-tracking"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<PollingSettings>(builder.Configuration.GetSection(PollingSettings.SectionName));
builder.Services.Configure<CarrierWebhookSettings>(
    builder.Configuration.GetSection(CarrierWebhookSettings.SectionName));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ShipmentCreatedHandler>();
builder.Services.AddScoped<ShipmentStatusChangedHandler>();

builder.Services.AddControllers();

// Consumes shipment events, polls couriers for status the couriers never pushed.
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<CarrierPollingWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
    await db.Database.MigrateAsync();
}

app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();
