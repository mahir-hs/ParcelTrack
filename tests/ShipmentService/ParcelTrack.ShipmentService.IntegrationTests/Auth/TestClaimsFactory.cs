using System.Text.Json;

namespace ParcelTrack.ShipmentService.IntegrationTests.Auth;

/// <summary>
/// Builds the claims dictionary that TestAuthHandler reads from the request header.
/// Each test can request a specific tenant/user identity.
/// </summary>
public static class TestClaimsFactory
{
    public static readonly Guid DefaultTenantId = Guid.NewGuid();
    public static readonly Guid DefaultUserId = Guid.NewGuid();

    public static readonly Guid AlternateTenantId = Guid.NewGuid();
    public static readonly Guid AlternateUserId = Guid.NewGuid();

    public static string DefaultClaims() => Build(DefaultTenantId, DefaultUserId);
    public static string AlternateClaims() => Build(AlternateTenantId, AlternateUserId);

    public static string Build(Guid tenantId, Guid userId)
    {
        var claims = new Dictionary<string, string>
        {
            ["sub"] = userId.ToString(),
            ["tenantId"] = tenantId.ToString()
        };

        return JsonSerializer.Serialize(claims);
    }
}