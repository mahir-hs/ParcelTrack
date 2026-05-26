using Microsoft.EntityFrameworkCore;
using ParcelTrack.WebhookDispatchService.Worker;
using ParcelTrack.WebhookDispatchService.Worker.Application;
using ParcelTrack.WebhookDispatchService.Worker.Infrastructure;
using ParcelTrack.WebhookDispatchService.Worker.Settings;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection("Kafka"));

builder.Services.AddDbContext<WebhookDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("WebhookDb"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IWebhookSubscriptionRepository, WebhookSubscriptionRepository>();
builder.Services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
builder.Services.AddScoped<WebhookDispatchHandler>();

builder.Services.AddHttpClient("webhook", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();
