using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParcelTrack.ShipmentService.API.Models;
using ParcelTrack.ShipmentService.Application.Commands;
using ParcelTrack.ShipmentService.Application.DTOs;
using ParcelTrack.ShipmentService.Application.Handler;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.API.Controllers;

/// <summary>
/// Inbound carrier status ingestion. Couriers (Pathao / Steadfast / RedX) POST status
/// updates here; we resolve the shipment by tracking number and apply the transition
/// through the same domain path the authenticated PUT /v1/shipments/{id}/status uses.
///
/// Protected by a shared carrier secret (X-Carrier-Secret) rather than a user JWT, so
/// it is explicitly [AllowAnonymous] to opt out of the global [Authorize] policy.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("v1/webhooks")]
public class CarrierWebhooksController : ControllerBase
{
    private readonly IShipmentRepository _repository;
    private readonly UpdateShipmentStatusCommandHandler _updateHandler;
    private readonly IConfiguration _configuration;

    public CarrierWebhooksController(
        IShipmentRepository repository,
        UpdateShipmentStatusCommandHandler updateHandler,
        IConfiguration configuration)
    {
        _repository = repository;
        _updateHandler = updateHandler;
        _configuration = configuration;
    }

    [HttpPost("{carrier}")]
    [ProducesResponseType(typeof(ShipmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Receive(
        string carrier,
        [FromHeader(Name = "X-Carrier-Secret")] string? secret,
        [FromBody] CarrierWebhookPayload payload,
        CancellationToken cancellationToken)
    {
        var expected = _configuration["CarrierWebhooks:Secret"];
        if (string.IsNullOrEmpty(expected) || secret != expected)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(payload.TrackingNumber) ||
            !Enum.TryParse<ShipmentStatus>(payload.Status, ignoreCase: true, out var newStatus))
            return BadRequest($"Invalid status '{payload.Status}'.");

        var shipment = await _repository.GetByTrackingNumberAsync(payload.TrackingNumber, cancellationToken);
        if (shipment is null)
            return NotFound();

        var command = new UpdateShipmentStatusCommand
        {
            ShipmentId = shipment.Id,
            TenantId = shipment.TenantId,
            NewStatus = newStatus,
            Description = payload.Description ?? $"Update from {carrier}",
            Location = payload.Location ?? string.Empty
        };

        var result = await _updateHandler.Handle(command, cancellationToken);
        return Ok(result);
    }
}
