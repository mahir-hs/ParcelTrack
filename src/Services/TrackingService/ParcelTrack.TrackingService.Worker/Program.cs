using ParcelTrack.TrackingService.Application.Handlers;
using ParcelTrack.TrackingService.Infrastructure;
using ParcelTrack.TrackingService.Worker;
using ParcelTrack.TrackingService.Worker.Settings;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka"));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ShipmentCreatedHandler>();
builder.Services.AddScoped<ShipmentStatusChangedHandler>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
