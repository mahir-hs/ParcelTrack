using ParcelTrack.TrackingService.Domain.Enums;

namespace ParcelTrack.TrackingService.Application.DTOs;

/// <summary>
/// What every carrier adapter returns, regardless of which courier produced it
/// or whether the status arrived by polling or by webhook push.
///
/// This is the boundary of the anti-corruption layer: courier-specific JSON,
/// status vocabularies, and quirks stop here.
/// </summary>
public sealed record CarrierTrackingResult
{
    /// <summary>The courier's consignment / tracking identifier.</summary>
    public required string TrackingNumber { get; init; }

    /// <summary>Normalised status. <see cref="CarrierStatus.Unknown"/> if the courier sent something unmapped.</summary>
    public required CarrierStatus Status { get; init; }

    /// <summary>
    /// The courier's own status string, kept verbatim.
    /// Preserved for audit and for diagnosing unmapped values in production logs.
    /// </summary>
    public required string RawStatus { get; init; }

    /// <summary>Human-readable description, safe to show to a buyer.</summary>
    public required string Description { get; init; }

    /// <summary>Hub or city, when the courier reports one.</summary>
    public string? Location { get; init; }

    /// <summary>When the courier says the status changed. Falls back to fetch time if absent.</summary>
    public required DateTime OccurredAt { get; init; }

    /// <summary>Which courier produced this result.</summary>
    public required CarrierType Carrier { get; init; }
}
