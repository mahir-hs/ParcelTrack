using Microsoft.EntityFrameworkCore;
using ParcelTrack.Shared.Messaging;
using ParcelTrack.TrackingService.Application.Handlers;
using ParcelTrack.TrackingService.Infrastructure;
using ParcelTrack.TrackingService.Infrastructure.Persistence;
using ParcelTrack.TrackingService.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Postgres read-model store
builder.Services.AddTrackingInfrastructure(builder.Configuration);

// Kafka consumer config (bound from "Kafka:Consumer") + dead-letter producer
builder.Services.Configure<KafkaConsumerOptions>(
    builder.Configuration.GetSection(KafkaConsumerOptions.SectionName));
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();

// Event handlers + the consumer host
builder.Services.AddScoped<ShipmentCreatedEventHandler>();
builder.Services.AddScoped<ShipmentStatusChangedEventHandler>();
builder.Services.AddHostedService<TrackingEventConsumer>();

var host = builder.Build();

// Ensure the read-model schema exists before consuming.
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();
    await db.Database.EnsureCreatedAsync();
}

await host.RunAsync();
