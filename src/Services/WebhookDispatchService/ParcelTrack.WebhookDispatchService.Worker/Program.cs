using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ParcelTrack.WebhookDispatchService.Worker;
using ParcelTrack.WebhookDispatchService.Worker.Application;
using ParcelTrack.WebhookDispatchService.Worker.Infrastructure;
using ParcelTrack.WebhookDispatchService.Worker.Settings;
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
    .ConfigureResource(r => r.AddService("parceltrack-webhook"))
    .WithTracing(t => t
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection("Kafka"));

builder.Services.AddDbContext<WebhookDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("WebhookDb"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddHealthChecks()
    .AddDbContextCheck<WebhookDbContext>("database");

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
