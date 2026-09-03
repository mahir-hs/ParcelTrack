namespace ParcelTrack.TrackingService.Infrastructure.Carriers.Pathao;

/// <summary>
/// Supplies a valid Pathao OAuth2 access token, fetching and caching as needed.
/// Separated from the adapter so token lifecycle can be tested on its own —
/// caching and refresh-on-expiry are where this kind of code usually goes wrong.
/// </summary>
public interface IPathaoTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the cached token so the next call re-authenticates.
    /// Called when Pathao answers 401 — the token may have been revoked before its stated expiry.
    /// </summary>
    void Invalidate();
}
