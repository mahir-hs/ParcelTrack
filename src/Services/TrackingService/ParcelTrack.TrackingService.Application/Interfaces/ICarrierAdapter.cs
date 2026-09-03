using ParcelTrack.TrackingService.Application.DTOs;
using ParcelTrack.TrackingService.Domain.Enums;

namespace ParcelTrack.TrackingService.Application.Interfaces;

/// <summary>
/// One implementation per courier. The polling worker and the webhook endpoints both
/// depend on this interface and never on a concrete courier — adding Steadfast or Redx
/// later means adding a class, not touching the pipeline.
/// </summary>
public interface ICarrierAdapter
{
    /// <summary>Which courier this adapter speaks for. Used to select an adapter at runtime.</summary>
    CarrierType Carrier { get; }

    /// <summary>True when the courier can push status changes to us. Redx, for example, cannot.</summary>
    bool SupportsWebhooks { get; }

    /// <summary>
    /// Fetches the current status for one consignment.
    /// Returns null when the courier has no record of the tracking number — that is an
    /// expected outcome, not a failure. Transport and courier faults throw instead.
    /// </summary>
    Task<CarrierTrackingResult?> GetStatusAsync(string trackingNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Translates a webhook body this courier posted to us.
    /// Returns null if the payload is unparseable or the courier does not support webhooks.
    /// </summary>
    CarrierTrackingResult? ParseWebhookPayload(string rawPayload);
}
