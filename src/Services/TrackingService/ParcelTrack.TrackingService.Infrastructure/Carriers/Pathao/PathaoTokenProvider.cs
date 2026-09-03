using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParcelTrack.TrackingService.Domain.Exceptions;

namespace ParcelTrack.TrackingService.Infrastructure.Carriers.Pathao;

/// <summary>
/// Fetches and caches Pathao OAuth2 tokens.
///
/// Registered as a singleton: one token is shared by every poll, which is the point.
/// The polling worker may check hundreds of consignments a minute, and re-authenticating
/// per request would be both slow and a good way to get rate-limited.
///
/// Concurrency: a SemaphoreSlim serialises refreshes so a burst of callers arriving on an
/// expired token produces one token request, not one per caller. Callers that arrive while
/// a refresh is in flight re-check the cache after acquiring the lock and usually find it filled.
/// </summary>
public sealed class PathaoTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<PathaoSettings> settings,
    TimeProvider timeProvider,
    ILogger<PathaoTokenProvider> logger) : IPathaoTokenProvider, IDisposable
{
    internal const string TokenEndpoint = "aladdin/api/v1/issue-token";

    private readonly PathaoSettings _settings = settings.Value;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (TryGetCachedToken(out var cached))
            return cached;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have refreshed while we waited for the lock.
            if (TryGetCachedToken(out cached))
                return cached;

            return await FetchTokenAsync(cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate()
    {
        _accessToken = null;
        _expiresAt = DateTimeOffset.MinValue;
    }

    private bool TryGetCachedToken(out string token)
    {
        var cached = _accessToken;

        if (cached is not null && timeProvider.GetUtcNow() < _expiresAt)
        {
            token = cached;
            return true;
        }

        token = string.Empty;
        return false;
    }

    private async Task<string> FetchTokenAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(PathaoAdapter.HttpClientName);

        var request = new PathaoTokenRequest(
            _settings.ClientId,
            _settings.ClientSecret,
            _settings.Username,
            _settings.Password);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(TokenEndpoint, request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new CarrierApiException("Pathao", "token request failed", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new CarrierApiException(
                "Pathao",
                $"token endpoint returned HTTP {(int)response.StatusCode}");
        }

        PathaoTokenResponse? token;
        try
        {
            token = await response.Content.ReadFromJsonAsync<PathaoTokenResponse>(cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new CarrierApiException("Pathao", "token response was not valid JSON", ex);
        }

        if (token?.AccessToken is null or "")
            throw new CarrierApiException("Pathao", "token response contained no access_token");

        // Expire early by the safety margin so a token never dies mid-request.
        var lifetime = Math.Max(0, token.ExpiresIn - _settings.TokenExpirySafetyMarginSeconds);

        _accessToken = token.AccessToken;
        _expiresAt = timeProvider.GetUtcNow().AddSeconds(lifetime);

        logger.LogInformation(
            "Acquired Pathao access token, valid for {Lifetime}s (raw expires_in {ExpiresIn}s)",
            lifetime, token.ExpiresIn);

        return token.AccessToken;
    }

    public void Dispose() => _refreshLock.Dispose();
}
