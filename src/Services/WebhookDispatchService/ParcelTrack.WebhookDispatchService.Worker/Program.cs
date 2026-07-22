using ParcelTrack.Shared.Messaging;
using ParcelTrack.WebhookDispatchService.Worker;
using ParcelTrack.WebhookDispatchService.Worker.Models;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("webhook");
builder.Services.Configure<WebhookOptions>(builder.Configuration.GetSection(WebhookOptions.SectionName));
builder.Services.Configure<KafkaConsumerOptions>(
    builder.Configuration.GetSection(KafkaConsumerOptions.SectionName));
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();

builder.Services.AddScoped<WebhookDispatcher>();
builder.Services.AddHostedService<WebhookEventConsumer>();

var host = builder.Build();
await host.RunAsync();
