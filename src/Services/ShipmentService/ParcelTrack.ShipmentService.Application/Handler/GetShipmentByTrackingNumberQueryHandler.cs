using Mapster;
using ParcelTrack.ShipmentService.Application.DTOs;
using ParcelTrack.ShipmentService.Application.Interfaces;

namespace ParcelTrack.ShipmentService.Application.Handler;

public class GetShipmentByTrackingNumberQueryHandler(IShipmentRepository repository)
{
    private readonly IShipmentRepository _repository = repository;

    public async Task<PublicTrackingDto?> Handle(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        var shipment = await _repository.GetByTrackingNumberPublicAsync(trackingNumber, cancellationToken);

        if (shipment is null)
            return null;

        return shipment.Adapt<PublicTrackingDto>();
    }
}
