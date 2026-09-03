using Microsoft.Extensions.Logging;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Entities;
using ParcelTrack.TrackingService.Domain.Enums;
using ParcelTrack.TrackingService.Domain.Exceptions;

namespace ParcelTrack.TrackingService.Application.Services;

/// <summary>
/// One polling cycle: ask each courier about the parcels it is carrying, and publish an event
/// for the ones that moved.
///
/// This is the piece that makes tracking automatic. Before it, a shipment only changed status
/// when someone called the API by hand; this asks the courier instead.
///
/// Deliberately not a BackgroundService — scheduling lives in the Worker, decisions live here,
/// so a full cycle can be tested without a host, a timer, or real time passing.
/// </summary>
public sealed class CarrierPollingService(
    IEnumerable<ICarrierAdapter> adapters,
    ITrackedShipmentRepository repository,
    CarrierObservationApplier applier,
    TimeProvider timeProvider,
    ILogger<CarrierPollingService> logger)
{
    private readonly Dictionary<CarrierType, ICarrierAdapter> _adapters =
        adapters.ToDictionary(a => a.Carrier);

    /// <summary>
    /// Polls up to <paramref name="batchSize"/> shipments per carrier and publishes any changes.
    /// Returns how many status changes were published.
    /// </summary>
    public async Task<int> PollAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var published = 0;

        foreach (var (carrier, adapter) in _adapters)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            published += await PollCarrierAsync(carrier, adapter, batchSize, cancellationToken);
        }

        return published;
    }

    private async Task<int> PollCarrierAsync(
        CarrierType carrier,
        ICarrierAdapter adapter,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var shipments = await repository.GetActiveByCarrierAsync(carrier, batchSize, cancellationToken);
        if (shipments.Count == 0)
            return 0;

        logger.LogInformation("Polling {Count} active {Carrier} shipments", shipments.Count, carrier);

        var published = 0;

        foreach (var shipment in shipments)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // One parcel's failure must not abandon the rest of the batch. Courier APIs fail
            // routinely, and the retry and circuit-breaker policies have already had their
            // turn by the time an exception surfaces here.
            try
            {
                if (await PollOneAsync(adapter, shipment, cancellationToken))
                    published++;
            }
            catch (CarrierApiException ex)
            {
                logger.LogWarning(ex,
                    "Skipping {TrackingNumber} this cycle — {Carrier} did not answer",
                    shipment.TrackingNumber, carrier);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Unexpected failure polling {TrackingNumber}", shipment.TrackingNumber);
            }
        }

        // Persist LastPolledAt for every parcel touched, changed or not — that timestamp is
        // what keeps the next cycle's ordering fair.
        await repository.SaveChangesAsync(cancellationToken);

        return published;
    }

    private async Task<bool> PollOneAsync(
        ICarrierAdapter adapter,
        TrackedShipment shipment,
        CancellationToken cancellationToken)
    {
        var observation = await adapter.GetStatusAsync(shipment.TrackingNumber, cancellationToken);

        if (observation is null)
        {
            // Booked with us but unknown to the courier — usually a parcel not yet handed over.
            shipment.TryRecordObservedStatus(CarrierStatus.Unknown, timeProvider.GetUtcNow().UtcDateTime);

            logger.LogWarning(
                "{Carrier} has no record of {TrackingNumber} — it may not be booked yet",
                shipment.CarrierType, shipment.TrackingNumber);
            return false;
        }

        return await applier.ApplyAsync(
            shipment,
            observation,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
    }
}
