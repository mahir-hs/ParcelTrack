using Microsoft.Extensions.Logging;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Enums;

namespace ParcelTrack.TrackingService.Application.Services;

/// <summary>
/// Handles a status push from a courier.
///
/// The fast path: the courier tells us the moment a parcel moves, instead of us discovering it
/// up to a poll interval later. Polling stays enabled as the safety net — pushes get lost, and
/// Redx cannot push at all.
/// </summary>
public sealed class CarrierWebhookService(
    IEnumerable<ICarrierAdapter> adapters,
    ITrackedShipmentRepository repository,
    CarrierObservationApplier applier,
    TimeProvider timeProvider,
    ILogger<CarrierWebhookService> logger)
{
    private readonly Dictionary<CarrierType, ICarrierAdapter> _adapters =
        adapters.ToDictionary(a => a.Carrier);

    public async Task<WebhookIngestOutcome> IngestAsync(
        CarrierType carrier,
        string rawPayload,
        CancellationToken cancellationToken = default)
    {
        if (!_adapters.TryGetValue(carrier, out var adapter))
        {
            logger.LogWarning("Received a webhook for {Carrier}, which has no adapter", carrier);
            return WebhookIngestOutcome.UnknownCarrier;
        }

        var observation = adapter.ParseWebhookPayload(rawPayload);
        if (observation is null)
            return WebhookIngestOutcome.Unparseable;

        var shipment = await repository.GetByTrackingNumberAsync(
            observation.TrackingNumber, cancellationToken);

        if (shipment is null)
        {
            // Couriers push for every parcel on the merchant account, including ones booked
            // outside ParcelTrack. Acknowledge and drop rather than erroring — a 4xx would
            // make the courier retry a parcel we will never recognise.
            logger.LogInformation(
                "Ignoring {Carrier} webhook for untracked consignment {TrackingNumber}",
                carrier, observation.TrackingNumber);
            return WebhookIngestOutcome.NotTracked;
        }

        var changed = await applier.ApplyAsync(
            shipment,
            observation,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        return changed ? WebhookIngestOutcome.Applied : WebhookIngestOutcome.NoChange;
    }
}

/// <summary>
/// What came of a webhook. Every value is a success from the courier's point of view —
/// each maps to a 2xx so the courier stops retrying. They differ only for our own metrics.
/// </summary>
public enum WebhookIngestOutcome
{
    /// <summary>New status, event published.</summary>
    Applied,

    /// <summary>Valid payload, but the status matched what we already had.</summary>
    NoChange,

    /// <summary>Consignment is not one we track.</summary>
    NotTracked,

    /// <summary>Body could not be read as this courier's format.</summary>
    Unparseable,

    /// <summary>No adapter is registered for the courier in the route.</summary>
    UnknownCarrier
}
