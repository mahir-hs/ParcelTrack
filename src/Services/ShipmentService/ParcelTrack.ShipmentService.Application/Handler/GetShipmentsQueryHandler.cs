using ParcelTrack.ShipmentService.Application.DTOs;
using ParcelTrack.ShipmentService.Application.DTOs.Common;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Application.Mappings;
using ParcelTrack.ShipmentService.Application.Queries;

namespace ParcelTrack.ShipmentService.Application.Handler;

public sealed class GetShipmentsQueryHandler(IShipmentRepository repository)
{
    private readonly IShipmentRepository _repository = repository;

    public async Task<PagedResult<ShipmentDto>> Handle(
        GetShipmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(
            query.TenantId,
            query.Page,
            query.PageSize,
            query.StatusFilter,
            cancellationToken);

        var dtos = items.ToDtoList();

        return new PagedResult<ShipmentDto>(dtos, query.Page, query.PageSize, totalCount);
    }
}
