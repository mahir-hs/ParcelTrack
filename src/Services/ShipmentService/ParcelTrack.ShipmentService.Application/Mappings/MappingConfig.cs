using Mapster;
using ParcelTrack.ShipmentService.Application.DTOs;
using ParcelTrack.ShipmentService.Domain.Entities;

namespace ParcelTrack.ShipmentService.Application.Mappings;

/// <summary>
/// Centralized Mapster configuration for the Application layer.
/// Called once at startup via DependencyInjection.cs.
///
/// Why explicit config instead of relying on Mapster's convention-based mapping?
/// - ShipmentDto.Events needs custom ordering (by OccurredAt)
/// - ShipmentSummaryDto is a projection — we explicitly ignore Events
/// - Makes all mapping behaviour visible in one place, no magic surprises
/// </summary>
public static class MappingConfig
{
    public static void Configure()
    {
        // ── Shipment → ShipmentDto ────────────────────────────────────────────
        // Full detail mapping including ordered event history
        TypeAdapterConfig<Shipment, ShipmentDto>.NewConfig()
            .Map(dest => dest.Events,
                 src => src.Events
                            .OrderBy(e => e.OccurredAt)
                            .Adapt<List<ShipmentEventDto>>());

        // ── Shipment → ShipmentSummaryDto ────────────────────────────────────
        // Lightweight projection for paginated list responses — no event history
        TypeAdapterConfig<Shipment, ShipmentSummaryDto>.NewConfig()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.TrackingNumber, src => src.TrackingNumber)
            .Map(dest => dest.CarrierType, src => src.CarrierType)
            .Map(dest => dest.Status, src => src.Status)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt)
            .Map(dest => dest.UpdatedAt, src => src.UpdatedAt);

        // ── ShipmentEvent → ShipmentEventDto ─────────────────────────────────
        // All fields map by convention (same names) — explicit config for clarity
        TypeAdapterConfig<ShipmentEvent, ShipmentEventDto>.NewConfig()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.ShipmentId, src => src.ShipmentId)
            .Map(dest => dest.Status, src => src.Status)
            .Map(dest => dest.Description, src => src.Description)
            .Map(dest => dest.Location, src => src.Location)
            .Map(dest => dest.OccurredAt, src => src.OccurredAt);

        TypeAdapterConfig<Shipment, PublicTrackingDto>.NewConfig()
            .Map(dest => dest.Carrier, src => src.CarrierType.ToString())
            .Map(dest => dest.CurrentStatus, src => src.Status.ToString())
            // Ensure events are sorted by newest first for the public view
            .Map(dest => dest.Events, src => src.Events.OrderByDescending(e => e.OccurredAt));

        TypeAdapterConfig<ShipmentEvent, PublicTrackingEventDto>.NewConfig()
            .Map(dest => dest.Status, src => src.Status.ToString());

    }
}