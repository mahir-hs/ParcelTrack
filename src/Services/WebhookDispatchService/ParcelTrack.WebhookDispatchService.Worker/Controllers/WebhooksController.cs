using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParcelTrack.WebhookDispatchService.Worker.Application;
using ParcelTrack.WebhookDispatchService.Worker.Domain;
using ParcelTrack.WebhookDispatchService.Worker.Infrastructure;

namespace ParcelTrack.WebhookDispatchService.Worker.Controllers;

[ApiController]
[Authorize]
[Route("v1/webhooks")]
public sealed class WebhooksController(
    IWebhookSubscriptionRepository repository,
    ITenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WebhookSubscriptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var subs = await repository.GetByTenantAsync(tenantContext.TenantId, cancellationToken);
        return Ok(subs.Select(WebhookSubscriptionDto.From));
    }

    [HttpPost]
    [ProducesResponseType(typeof(WebhookSubscriptionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWebhookSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetUrl) || !Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out _))
            return BadRequest("TargetUrl must be a valid absolute URL.");

        var subscription = WebhookSubscription.Create(
            tenantContext.TenantId,
            request.TargetUrl,
            request.Secret);

        await repository.AddAsync(subscription, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        var dto = WebhookSubscriptionDto.From(subscription);
        return CreatedAtAction(nameof(List), dto);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var subscription = await repository.GetByIdAsync(id, tenantContext.TenantId, cancellationToken);
        if (subscription is null) return NotFound();

        subscription.Deactivate();
        await repository.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}

public sealed record CreateWebhookSubscriptionRequest(string TargetUrl, string? Secret);

public sealed record WebhookSubscriptionDto(Guid Id, string TargetUrl, bool IsActive, DateTime CreatedAt)
{
    public static WebhookSubscriptionDto From(WebhookSubscription s) =>
        new(s.Id, s.TargetUrl, s.IsActive, s.CreatedAt);
}
