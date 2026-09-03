using Microsoft.Extensions.Options;
using ParcelTrack.TrackingService.Application.Services;
using ParcelTrack.TrackingService.Worker.Settings;

namespace ParcelTrack.TrackingService.Worker;

/// <summary>
/// Runs a carrier poll on a fixed interval.
///
/// Scheduling only — every decision lives in CarrierPollingService. A BackgroundService is a
/// singleton while the polling service needs scoped dependencies (DbContext), so each cycle
/// opens its own scope; that also means one cycle's failure cannot poison the next one's state.
/// </summary>
public sealed class CarrierPollingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PollingSettings> options,
    ILogger<CarrierPollingWorker> logger) : BackgroundService
{
    private readonly PollingSettings _settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            logger.LogInformation("Carrier polling is disabled — running as a consumer only");
            return;
        }

        logger.LogInformation(
            "Carrier polling starts in {Delay}s, then every {Interval}s (batch {BatchSize} per carrier)",
            _settings.StartupDelaySeconds, _settings.IntervalSeconds, _settings.BatchSize);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_settings.StartupDelaySeconds), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_settings.IntervalSeconds));

        do
        {
            await RunCycleAsync(stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));

        logger.LogInformation("Carrier polling stopped");
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var poller = scope.ServiceProvider.GetRequiredService<CarrierPollingService>();

            var published = await poller.PollAsync(_settings.BatchSize, stoppingToken);

            if (published > 0)
                logger.LogInformation("Poll cycle published {Count} status changes", published);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown, not a fault.
        }
        catch (Exception ex)
        {
            // A cycle must never take the worker down — the next one may well succeed.
            logger.LogError(ex, "Poll cycle failed");
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
