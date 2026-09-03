using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ParcelTrack.NotificationService.Application.Handlers;
using ParcelTrack.NotificationService.Application.Interfaces;
using ParcelTrack.NotificationService.Worker;
using ParcelTrack.NotificationService.Worker.Notifications;
using ParcelTrack.NotificationService.Worker.Settings;
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
    .ConfigureResource(r => r.AddService("parceltrack-notification"))
    .WithTracing(t => t
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection("Kafka"));

builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("Smtp"));

builder.Services.AddSingleton<INotificationSender, SmtpNotificationSender>();
builder.Services.AddSingleton<ShipmentCreatedHandler>();
builder.Services.AddSingleton<ShipmentStatusChangedHandler>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
