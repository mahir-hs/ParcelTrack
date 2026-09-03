using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ParcelTrack.TrackingService.Application.Services;
using ParcelTrack.TrackingService.Domain.Enums;
using ParcelTrack.TrackingService.Worker.Settings;

namespace ParcelTrack.TrackingService.Worker.Controllers;

/// <summary>
/// Receives status pushes from couriers.
///
/// Deliberately unauthenticated in the JWT sense — couriers do not hold ParcelTrack tokens.
/// Each route is guarded by a shared secret the courier sends back in a header, configured
/// per carrier when the webhook is registered with them.
///
/// Every outcome that is not "you sent us nonsense" answers 2xx. A courier receiving a 4xx or
/// 5xx will retry, and there is nothing to gain from making it retry a consignment we do not
/// track or a status we already knew.
/// </summary>
[ApiController]
[Route("webhooks")]
public sealed class CarrierWebhooksController(
    CarrierWebhookService webhookService,
    IOptions<CarrierWebhookSettings> settings,
    ILogger<CarrierWebhooksController> logger) : ControllerBase
{
    private readonly CarrierWebhookSettings _settings = settings.Value;

    [HttpPost("pathao")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Pathao(CancellationToken cancellationToken) =>
        HandleAsync(CarrierType.Pathao, _settings.PathaoSecret, cancellationToken);

    private async Task<IActionResult> HandleAsync(
        CarrierType carrier,
        string? expectedSecret,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorised(expectedSecret))
        {
            logger.LogWarning("Rejected a {Carrier} webhook with a missing or wrong secret", carrier);
            return Unauthorized();
        }

        string body;
        using (var reader = new StreamReader(Request.Body))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        var outcome = await webhookService.IngestAsync(carrier, body, cancellationToken);

        return outcome switch
        {
            WebhookIngestOutcome.Unparseable => BadRequest(new { message = "Payload could not be read." }),
            _ => Accepted(new { outcome = outcome.ToString() })
        };
    }

    /// <summary>
    /// Compares the courier's secret header in constant time.
    ///
    /// When no secret is configured the check is skipped, which keeps local development
    /// workable — production must set one, or anyone who learns the URL can post fake statuses.
    /// </summary>
    private bool IsAuthorised(string? expectedSecret)
    {
        if (string.IsNullOrWhiteSpace(expectedSecret))
        {
            logger.LogWarning(
                "No webhook secret configured — accepting unverified carrier callbacks. Do not run like this in production.");
            return true;
        }

        if (!Request.Headers.TryGetValue(CarrierWebhookSettings.SecretHeaderName, out var provided))
            return false;

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(provided.ToString()),
            System.Text.Encoding.UTF8.GetBytes(expectedSecret));
    }
}
