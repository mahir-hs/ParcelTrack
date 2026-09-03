using ParcelTrack.TrackingService.Domain.Enums;

namespace ParcelTrack.TrackingService.Infrastructure.Carriers.Pathao;

/// <summary>
/// Translates Pathao's status vocabulary into ParcelTrack's.
///
/// Slugs are normalised before lookup — lowercased with separators stripped — because
/// couriers are inconsistent about casing and punctuation between the REST API,
/// their webhooks, and their dashboard ("Assigned_for_Delivery", "assigned-for-delivery").
/// Normalising means one entry per concept instead of one per spelling.
///
/// An unrecognised slug maps to <see cref="CarrierStatus.Unknown"/> rather than throwing.
/// A courier adding a new state should never take the polling worker down; the raw value
/// is preserved on the result so unmapped statuses surface in logs and can be added here.
/// </summary>
internal static class PathaoStatusMapper
{
    private static readonly Dictionary<string, CarrierStatus> Map = new()
    {
        // Booked, not yet collected from the merchant
        ["pickuprequested"] = CarrierStatus.Created,
        ["assignedforpickup"] = CarrierStatus.Created,

        // Collected and moving through the network
        ["picked"] = CarrierStatus.InTransit,
        ["pickedup"] = CarrierStatus.InTransit,
        ["atthesortinghub"] = CarrierStatus.InTransit,
        ["intransit"] = CarrierStatus.InTransit,
        ["receivedatlastmilehub"] = CarrierStatus.InTransit,

        // With the rider
        ["assignedfordelivery"] = CarrierStatus.OutForDelivery,
        ["outfordelivery"] = CarrierStatus.OutForDelivery,

        // Done
        ["delivered"] = CarrierStatus.Delivered,
        ["partialdelivery"] = CarrierStatus.Delivered,
        ["deliveredapprovalpending"] = CarrierStatus.Delivered,

        // Attempted and failed — retryable, not terminal
        ["pickupfailed"] = CarrierStatus.Failed,
        ["deliveryfailed"] = CarrierStatus.Failed,
        ["onhold"] = CarrierStatus.Failed,

        // Heading back to the merchant
        ["return"] = CarrierStatus.Returned,
        ["returned"] = CarrierStatus.Returned,
        ["returnedtomerchant"] = CarrierStatus.Returned,
        ["paymentinvoice"] = CarrierStatus.Returned,

        // Called off
        ["cancelled"] = CarrierStatus.Cancelled,
        ["pickupcancelled"] = CarrierStatus.Cancelled,
        ["deliverycancelled"] = CarrierStatus.Cancelled
    };

    /// <summary>Maps a Pathao slug or display status. Null, blank, and unknown values map to Unknown.</summary>
    public static CarrierStatus ToCarrierStatus(string? pathaoStatus)
    {
        if (string.IsNullOrWhiteSpace(pathaoStatus))
            return CarrierStatus.Unknown;

        return Map.TryGetValue(Normalise(pathaoStatus), out var status)
            ? status
            : CarrierStatus.Unknown;
    }

    /// <summary>Lowercases and drops every non-alphanumeric character.</summary>
    private static string Normalise(string value)
    {
        Span<char> buffer = value.Length <= 128 ? stackalloc char[value.Length] : new char[value.Length];
        var length = 0;

        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
                buffer[length++] = char.ToLowerInvariant(c);
        }

        return new string(buffer[..length]);
    }
}
