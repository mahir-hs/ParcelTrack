using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ParcelTrack.ShipmentService.API.Infrastructure;

/// <summary>
/// Development-only authentication scheme.
///
/// Reads the tenant/user identity from request headers so the API can be exercised
/// without a running Keycloak:
///   X-Tenant-Id : GUID of the tenant
///   X-User-Id   : GUID of the user
///
/// When neither header is present it falls back to a fixed "demo" tenant/user so that
/// manual exploration (e.g. the .http file) works out of the box.
///
/// Disabled automatically when Auth:DevMode is false — production uses Keycloak JWTs.
/// </summary>
public sealed class DevAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Dev";

    // Default user identity used when only X-Tenant-Id is supplied.
    private static readonly Guid DemoUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public DevAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // No identity supplied → fail (401). This keeps [Authorize] meaningful in dev:
        // requests must carry at least an X-Tenant-Id header (X-User-Id defaults).
        if (!Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) ||
            !Guid.TryParse(tenantHeader.ToString(), out var tenantId))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing or invalid X-Tenant-Id header."));
        }

        Guid userId = DemoUserId;
        if (Request.Headers.TryGetValue("X-User-Id", out var userHeader) &&
            Guid.TryParse(userHeader.ToString(), out var parsedUser))
        {
            userId = parsedUser;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new("tenantId", tenantId.ToString())
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
