using Mapster;
using ParcelTrack.ShipmentService.Application.DTOs;
using ParcelTrack.ShipmentService.Domain.Entities;

namespace ParcelTrack.ShipmentService.Application.Mappings;

/// <summary>
/// Extension methods that give handlers a clean ToDto() API.
/// Mapster does the actual mapping work — callers never touch .Adapt directly.
///
/// Usage in handlers:
///   shipment.ToDto()
///   shipment.ToSummaryDto()
///   items.ToDtoList()
///   items.ToSummaryList()
/// </summary>
public static class ShipmentMappingExtensions
{
    public static ShipmentDto ToDto(this Shipment shipment)
        => shipment.Adapt<ShipmentDto>();

    public static ShipmentSummaryDto ToSummaryDto(this Shipment shipment)
        => shipment.Adapt<ShipmentSummaryDto>();

    public static ShipmentEventDto ToDto(this ShipmentEvent shipmentEvent)
        => shipmentEvent.Adapt<ShipmentEventDto>();

    public static IReadOnlyList<ShipmentDto> ToDtoList(this IEnumerable<Shipment> shipments)
        => shipments.Select(s => s.ToDto()).ToList().AsReadOnly();

    public static IReadOnlyList<ShipmentSummaryDto> ToSummaryList(this IEnumerable<Shipment> shipments)
        => shipments.Select(s => s.ToSummaryDto()).ToList().AsReadOnly();
}