namespace ParcelTrack.TrackingService.Domain.Exceptions;

/// <summary>
/// A courier's API refused, failed, or answered with something we could not read.
///
/// Distinct from "no such tracking number", which is a null result rather than an
/// exception — that is a normal answer, this is a fault worth retrying or tripping a breaker on.
/// </summary>
public sealed class CarrierApiException(string carrier, string message, Exception? innerException = null)
    : Exception($"{carrier}: {message}", innerException)
{
    public string Carrier { get; } = carrier;
}
