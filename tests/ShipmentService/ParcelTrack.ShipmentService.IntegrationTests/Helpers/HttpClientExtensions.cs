using ParcelTrack.ShipmentService.IntegrationTests.Auth;

namespace ParcelTrack.ShipmentService.IntegrationTests.Helpers;

/// <summary>
/// Attaches test auth claims to an HttpRequestMessage.
/// Keeps test code readable — client.WithClaims(TestClaimsFactory.DefaultClaims())
/// </summary>
public static class HttpClientExtensions
{
    public static HttpClient WithClaims(this HttpClient client, string claimsJson)
    {
        client.DefaultRequestHeaders.Remove(TestAuthHandler.ClaimsHeader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.ClaimsHeader, claimsJson);
        return client;
    }
}