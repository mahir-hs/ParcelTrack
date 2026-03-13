using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.Application.Commands;

public sealed record CreateShipmentCommand
{
    public string      TrackingNumber { get; init; } = string.Empty;
    public CarrierType CarrierType    { get; init; }
    public string?     BuyerEmail     { get; init; }
    public Guid        UserId         { get; init; }
    public Guid        TenantId       { get; init; }
}