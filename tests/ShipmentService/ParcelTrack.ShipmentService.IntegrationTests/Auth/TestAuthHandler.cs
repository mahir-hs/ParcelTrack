using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ParcelTrack.ShipmentService.IntegrationTests.Auth;

/// <summary>
/// Replaces Keycloak JWT validation in the test host.
/// Reads claims from the request header "X-Test-Claims" (injected by AuthHelper).
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";
    public const string ClaimsHeader = "X-Test-Claims";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ClaimsHeader, out var claimsJson))
            return Task.FromResult(AuthenticateResult.Fail("No test claims header"));

        var claims = System.Text.Json.JsonSerializer
            .Deserialize<Dictionary<string, string>>(claimsJson.ToString())!
            .Select(kv => new Claim(kv.Key, kv.Value))
            .ToList();

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}