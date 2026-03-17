using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParcelTrack.ShipmentService.API.Models;
using ParcelTrack.ShipmentService.Application.Commands;
using ParcelTrack.ShipmentService.Application.DTOs;
using ParcelTrack.ShipmentService.Application.Handler;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Application.Queries;
using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.API.Controllers;

[ApiController]
[Authorize]
[Route("v1/shipments")]
public class ShipmentsController : ControllerBase
{
    private readonly CreateShipmentCommandHandler _createHandler;
    private readonly UpdateShipmentStatusCommandHandler _updateHandler;
    private readonly CancelShipmentCommandHandler _cancelHandler;
    private readonly GetShipmentByIdQueryHandler _getByIdHandler;
    private readonly GetShipmentsQueryHandler _getPagedHandler;
    private readonly ITenantContext _tenantContext;

    public ShipmentsController(
        CreateShipmentCommandHandler createHandler,
        UpdateShipmentStatusCommandHandler updateHandler,
        CancelShipmentCommandHandler cancelHandler,
        GetShipmentByIdQueryHandler getByIdHandler,
        GetShipmentsQueryHandler getPagedHandler,
        ITenantContext tenantContext)
    {
        _createHandler = createHandler;
        _updateHandler = updateHandler;
        _cancelHandler = cancelHandler;
        _getByIdHandler = getByIdHandler;
        _getPagedHandler = getPagedHandler;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Register a new shipment for tracking.
    /// TenantId and UserId are resolved from the JWT — never from the request body.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ShipmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateShipmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CarrierType>(request.CarrierType, ignoreCase: true, out var carrierType))
            return BadRequest($"Invalid carrier type '{request.CarrierType}'.");

        var command = new CreateShipmentCommand
        {
            TrackingNumber = request.TrackingNumber,
            CarrierType = carrierType,
            BuyerEmail = request.BuyerEmail,
            UserId = _tenantContext.UserId,
            TenantId = _tenantContext.TenantId
        };

        var result = await _createHandler.Handle(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get a shipment by ID. Tenant-scoped via global query filter.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ShipmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetShipmentByIdQuery
        {
            ShipmentId = id,
            TenantId = _tenantContext.TenantId
        };

        var result = await _getByIdHandler.Handle(query, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Get paginated list of shipments for the current tenant.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ShipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] ShipmentStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetShipmentsQuery
        {
            Page = page,
            PageSize = pageSize,
            StatusFilter = status
        };

        var result = await _getPagedHandler.Handle(query, cancellationToken);

        return Ok(new PagedResponse<ShipmentDto>
        {
            Items = result.Items.ToList(),
            TotalCount = result.TotalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Update shipment status. Used by carrier adapters and tracking service.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(ShipmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateShipmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ShipmentStatus>(request.NewStatus, ignoreCase: true, out var newStatus))
            return BadRequest($"Invalid status '{request.NewStatus}'.");

        var command = new UpdateShipmentStatusCommand
        {
            ShipmentId = id,
            TenantId = _tenantContext.TenantId,
            NewStatus = newStatus,
            Description = request.Description,
            Location = request.Location ?? string.Empty
        };

        var result = await _updateHandler.Handle(command, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Cancel a shipment. Only the owning user can cancel.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ShipmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelShipmentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CancelShipmentCommand
        {
            ShipmentId = id,
            TenantId = _tenantContext.TenantId,
            RequestingUserId = _tenantContext.UserId,
            Reason = request.Reason
        };

        var result = await _cancelHandler.Handle(command, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Get full event history for a shipment.
    /// </summary>
    [HttpGet("{id:guid}/events")]
    [ProducesResponseType(typeof(IReadOnlyList<ShipmentEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetEvents(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetShipmentByIdQuery
        {
            ShipmentId = id,
            TenantId = _tenantContext.TenantId
        };

        var result = await _getByIdHandler.Handle(query, cancellationToken);

        return result is null ? NotFound() : Ok(result.Events);
    }
}
