using System.Security.Claims;
using ParcelTrack.WebhookDispatchService.Worker.Application;

namespace ParcelTrack.WebhookDispatchService.Worker.Infrastructure;

public sealed class TenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    private Guid? _tenantId;
    private Guid? _userId;

    public Guid TenantId => _tenantId ??= ResolveTenantId();
    public Guid UserId => _userId ??= ResolveUserId();

    private ClaimsPrincipal GetUser() =>
        httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("TenantContext cannot be resolved outside of an HTTP request.");

    private Guid ResolveTenantId()
    {
        var value = GetUser().FindFirst("tenantId")?.Value
            ?? throw new InvalidOperationException("JWT is missing the 'tenantId' claim.");
        return Guid.Parse(value);
    }

    private Guid ResolveUserId()
    {
        var user = GetUser();
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? user.FindFirst("sub")?.Value
                 ?? throw new InvalidOperationException("JWT is missing the 'sub' claim.");
        return Guid.Parse(value);
    }
}
