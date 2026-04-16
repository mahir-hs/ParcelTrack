using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParcelTrack.ShipmentService.Application.DTOs;
using ParcelTrack.ShipmentService.Application.Handler;

namespace ParcelTrack.ShipmentService.API.Controllers;

[ApiController]
[Route("v1/track")]
[AllowAnonymous]
public sealed class TrackingController : ControllerBase
{
    private readonly GetShipmentByTrackingNumberQueryHandler _getByTrackingIdHandler;

    public TrackingController(GetShipmentByTrackingNumberQueryHandler getByTrackingIdHandler)
    {
        _getByTrackingIdHandler = getByTrackingIdHandler;
    }

    /// <summary>
    /// Public tracking endpoint — no authentication required.
    /// Returns shipment status and event history for any valid tracking number.
    /// Never exposes TenantId, UserId, or any internal business data.
    /// </summary>
    [HttpGet("{trackingNumber}")]
    [ProducesResponseType(typeof(PublicTrackingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Track(string trackingNumber, CancellationToken cancellationToken)
    {
        var result = await _getByTrackingIdHandler.Handle(trackingNumber, cancellationToken);
        if (result is null)
        {
            return NotFound(new { message = $"Tracking number '{trackingNumber}' not found." });
        }

        return Ok(result);
    }
}