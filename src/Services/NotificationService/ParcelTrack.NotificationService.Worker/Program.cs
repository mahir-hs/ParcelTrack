using ParcelTrack.NotificationService.Application.Handlers;
using ParcelTrack.NotificationService.Application.Interfaces;
using ParcelTrack.NotificationService.Worker;
using ParcelTrack.NotificationService.Worker.Notifications;
using ParcelTrack.NotificationService.Worker.Settings;

// LogNotificationSender is kept as a fallback for local dev without SMTP configured.
// Switch INotificationSender registration below to use it.

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection("Kafka"));

builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("Smtp"));

builder.Services.AddScoped<INotificationSender, SmtpNotificationSender>();
builder.Services.AddScoped<ShipmentCreatedHandler>();
builder.Services.AddScoped<ShipmentStatusChangedHandler>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
