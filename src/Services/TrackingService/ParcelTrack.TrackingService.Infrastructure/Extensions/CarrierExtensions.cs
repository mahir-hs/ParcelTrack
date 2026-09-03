using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Infrastructure.Carriers.Pathao;
using Polly;
using Polly.Timeout;

namespace ParcelTrack.TrackingService.Infrastructure.Extensions;

public static class CarrierExtensions
{
    /// <summary>
    /// Registers carrier adapters and their HTTP pipelines.
    ///
    /// Resilience lives here rather than inside the adapters so every carrier gets the same
    /// guarantees and no adapter can quietly forget them. The policy order matters:
    /// the per-attempt timeout sits inside retry (each attempt gets its own budget),
    /// and the circuit breaker wraps both — so a courier that is comprehensively down
    /// stops being called at all instead of absorbing three retries per parcel.
    /// </summary>
    public static IServiceCollection AddCarriers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PathaoSettings>(configuration.GetSection(PathaoSettings.SectionName));

        // Injected rather than DateTime.UtcNow so token expiry is testable without sleeping.
        services.TryAddSingletonTimeProvider();

        var settings = configuration.GetSection(PathaoSettings.SectionName).Get<PathaoSettings>()
                       ?? new PathaoSettings();

        services.AddHttpClient(PathaoAdapter.HttpClientName, client =>
            {
                client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds * 3);
            })
            .AddResilienceHandler("pathao", builder =>
            {
                builder
                    .AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromSeconds(1),
                        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                            .Handle<HttpRequestException>()
                            .Handle<TimeoutRejectedException>()
                            .HandleResult(static r => r.StatusCode is System.Net.HttpStatusCode.RequestTimeout
                                or System.Net.HttpStatusCode.TooManyRequests
                                or >= System.Net.HttpStatusCode.InternalServerError)
                    })
                    .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions<HttpResponseMessage>
                    {
                        // Trip once half the calls in the window fail, then stop calling for 30s.
                        FailureRatio = 0.5,
                        MinimumThroughput = 5,
                        SamplingDuration = TimeSpan.FromSeconds(30),
                        BreakDuration = TimeSpan.FromSeconds(30),
                        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                            .Handle<HttpRequestException>()
                            .Handle<TimeoutRejectedException>()
                            .HandleResult(static r => r.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
                    })
                    .AddTimeout(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            });

        services.AddSingleton<IPathaoTokenProvider, PathaoTokenProvider>();
        services.AddSingleton<ICarrierAdapter, PathaoAdapter>();

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
            services.AddSingleton(TimeProvider.System);
    }
}
