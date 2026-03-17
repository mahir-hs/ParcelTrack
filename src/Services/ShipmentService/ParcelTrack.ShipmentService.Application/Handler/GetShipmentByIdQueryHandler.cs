using ParcelTrack.ShipmentService.Application.DTOs;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Application.Mappings;
using ParcelTrack.ShipmentService.Application.Queries;
using ParcelTrack.ShipmentService.Domain.Exceptions;

namespace ParcelTrack.ShipmentService.Application.Handler;

public sealed class GetShipmentByIdQueryHandler(IShipmentRepository repository)
{
    private readonly IShipmentRepository _repository = repository;

    public async Task<ShipmentDto> Handle(
        GetShipmentByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var shipment = await _repository.GetByIdAsync(
            query.ShipmentId,
            cancellationToken)
            ?? throw new ShipmentNotFoundException(query.ShipmentId);

        return shipment.ToDto();
    }
}
