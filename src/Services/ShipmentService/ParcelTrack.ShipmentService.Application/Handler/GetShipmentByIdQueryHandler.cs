using ParcelTrack.ShipmentService.Application.DTOs;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Application.Mappings;
using ParcelTrack.ShipmentService.Application.Queries;

namespace ParcelTrack.ShipmentService.Application.Handler;

public sealed class GetShipmentByIdQueryHandler(IShipmentRepository repository)
{
    private readonly IShipmentRepository _repository = repository;

    public async Task<ShipmentDto> Handle(
        GetShipmentByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var shipment = await _repository.GetByIdAsyncWithEvents(
            query.ShipmentId,
            cancellationToken);

        return shipment.ToDto();
    }
}
