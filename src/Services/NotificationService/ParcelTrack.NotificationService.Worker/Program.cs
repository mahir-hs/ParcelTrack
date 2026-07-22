using Microsoft.EntityFrameworkCore;
using ParcelTrack.NotificationService.Application;
using ParcelTrack.NotificationService.Application.Handlers;
using ParcelTrack.NotificationService.Application.Persistence;
using ParcelTrack.NotificationService.Worker;
using ParcelTrack.Shared.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddNotificationInfrastructure(builder.Configuration);

builder.Services.Configure<KafkaConsumerOptions>(
    builder.Configuration.GetSection(KafkaConsumerOptions.SectionName));
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();

builder.Services.AddScoped<ShipmentStatusChangedEventHandler>();
builder.Services.AddHostedService<NotificationEventConsumer>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.EnsureCreatedAsync();
}

await host.RunAsync();
