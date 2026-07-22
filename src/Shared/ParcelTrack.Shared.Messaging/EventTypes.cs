using ParcelTrack.Shared.Contracts.Events;

namespace ParcelTrack.Shared.Messaging;

/// <summary>
/// Maps the event-type string (written by the ShipmentService outbox as typeof(T).FullName)
/// back to its CLR type so the consumer can JSON-deserialize the payload.
///
/// Add new event records here as they are introduced. This is the single source of truth
/// shared by every consumer in the system.
/// </summary>
public static class EventTypes
{
    private static readonly Dictionary<string, Type> Map = new(StringComparer.Ordinal)
    {
        [typeof(ShipmentCreatedEvent).FullName!] = typeof(ShipmentCreatedEvent),
        [typeof(ShipmentStatusChangedEvent).FullName!] = typeof(ShipmentStatusChangedEvent),
    };

    public static bool TryResolve(string typeName, out Type type) =>
        Map.TryGetValue(typeName, out type!);
}
