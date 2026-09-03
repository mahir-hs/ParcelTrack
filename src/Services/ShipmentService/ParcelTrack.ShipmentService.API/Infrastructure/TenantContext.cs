using System.Security.Claims;
using ParcelTrack.ShipmentService.Application.Interfaces;

namespace ParcelTrack.ShipmentService.API.Infrastructure;

/// <summary>
/// Resolves TenantId and UserId for the current unit of work — from an explicit assignment
/// via ITenantContextSetter if one was made, otherwise from the HTTP request's JWT claims.
/// Background consumers use the former: a Kafka message carries its own TenantId and there is
/// no token to read. Scoped, so an explicit assignment cannot leak into another request.
/// Registered as scoped — one instance per request, disposed after the request ends.
///
/// Claims are resolved lazily on first access rather than in the constructor.
/// This allows the type to be safely instantiated during startup (e.g. EF migrations)
/// without an active HTTP context — the exception only fires if claims are actually read
/// outside a request, which no legitimate code path does.
/// </summary>
public sealed class TenantContext : ITenantContext, ITenantContextSetter
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _tenantId;
    private Guid? _userId;

    /// <summary>True once SetContext has pinned this scope, which suppresses claim lookup.</summary>
    private bool _explicitlySet;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId => _explicitlySet ? _tenantId!.Value : _tenantId ??= ResolveTenantId();
    public Guid UserId => _explicitlySet ? _userId!.Value : _userId ??= ResolveUserId();

    /// <summary>
    /// Pins this scope to a tenant for work with no HTTP request behind it.
    /// A Kafka message carries its own TenantId; the consumer states it before calling a handler.
    /// </summary>
    public void SetContext(Guid tenantId, Guid userId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A background tenant context needs a real TenantId.", nameof(tenantId));

        _tenantId = tenantId;
        _userId = userId;
        _explicitlySet = true;
    }

    private ClaimsPrincipal GetAuthenticatedUser()
    {
        var context = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "TenantContext cannot be resolved outside of an HTTP request. "
                + "Background work must call ITenantContextSetter.SetContext first.");

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