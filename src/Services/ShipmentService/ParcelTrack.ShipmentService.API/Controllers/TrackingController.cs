using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParcelTrack.ShipmentService.Application.Interfaces;

namespace ParcelTrack.ShipmentService.API.Controllers;

[ApiController]
[Route("v1/track")]
[AllowAnonymous]
public sealed class TrackingController : ControllerBase
{
    private readonly IShipmentRepository _repository;

    public TrackingController(IShipmentRepository repository)
    {
        _repository = repository;
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
        var shipment = await _repository.GetByTrackingNumberPublicAsync(trackingNumber, cancellationToken);

        if (shipment is null)
            return NotFound(new { message = $"Tracking number '{trackingNumber}' not found." });

        var response = new PublicTrackingDto
        {
            TrackingNumber = shipment.TrackingNumber,
            Carrier = shipment.CarrierType.ToString(),
            CurrentStatus = shipment.Status.ToString(),
            DestinationCity = shipment.DestinationCity,
            Events = shipment.Events
                .OrderBy(e => e.OccurredAt)
                .Select(e => new PublicTrackingEventDto
                {
                    Status = e.Status.ToString(),
                    Description = e.Description,
                    Location = e.Location,
                    OccurredAt = e.OccurredAt
                })
                .ToList()
        };

        return Ok(response);
    }
}
/// <summary>
/// Public-safe projection — contains zero internal identifiers.
/// This is all a buyer ever sees.
/// </summary>
public sealed record PublicTrackingDto
{
    public string TrackingNumber { get; init; } = string.Empty;
    public string Carrier { get; init; } = string.Empty;
    public string CurrentStatus { get; init; } = string.Empty;
    public string? DestinationCity { get; init; }
    public List<PublicTrackingEventDto> Events { get; init; } = [];
}

public sealed record PublicTrackingEventDto
{
    public string Status { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Location { get; init; }
    public DateTime OccurredAt { get; init; }
}