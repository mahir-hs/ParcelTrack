using ParcelTrack.NotificationService.Application.Handlers;
using ParcelTrack.NotificationService.Application.Interfaces;
using ParcelTrack.NotificationService.Worker;
using ParcelTrack.NotificationService.Worker.Notifications;
using ParcelTrack.NotificationService.Worker.Settings;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection("Kafka"));

builder.Services.AddScoped<INotificationSender, LogNotificationSender>();
builder.Services.AddScoped<ShipmentCreatedHandler>();
builder.Services.AddScoped<ShipmentStatusChangedHandler>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
