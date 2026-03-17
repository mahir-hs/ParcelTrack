using System.Security.Claims;
using ParcelTrack.ShipmentService.Application.Interfaces;

namespace ParcelTrack.ShipmentService.API.Infrastructure;

/// <summary>
/// Resolves TenantId and UserId from the current HTTP request's JWT claims.
/// Registered as scoped — one instance per request, disposed after the request ends.
///
/// Claims are resolved lazily on first access rather than in the constructor.
/// This allows the type to be safely instantiated during startup (e.g. EF migrations)
/// without an active HTTP context — the exception only fires if claims are actually read
/// outside a request, which no legitimate code path does.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _tenantId;
    private Guid? _userId;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId => _tenantId ??= ResolveTenantId();
    public Guid UserId => _userId ??= ResolveUserId();

    private ClaimsPrincipal GetAuthenticatedUser()
    {
        var context = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "TenantContext cannot be resolved outside of an HTTP request.");

        return context.User;
    }

    private Guid ResolveTenantId()
    {
        var claim = GetAuthenticatedUser().FindFirst("tenantId")?.Value
            ?? throw new InvalidOperationException("JWT is missing the 'tenantId' claim.");

        return Guid.Parse(claim);
    }

    private Guid ResolveUserId()
    {
        var user = GetAuthenticatedUser();

        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? user.FindFirst("sub")?.Value
                 ?? throw new InvalidOperationException("JWT is missing the 'sub' claim.");

        return Guid.Parse(claim);
    }
}