using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParcelTrack.TrackingService.Application.DTOs;
using ParcelTrack.TrackingService.Application.Interfaces;
using ParcelTrack.TrackingService.Domain.Enums;
using ParcelTrack.TrackingService.Domain.Exceptions;

namespace ParcelTrack.TrackingService.Infrastructure.Carriers.Pathao;

/// <summary>
/// Talks to Pathao Courier's merchant API.
///
/// Pathao is the first real carrier integration because it is the only one of the three
/// with a public sandbox — https://courier-api-sandbox.pathao.com — so the OAuth2 flow,
/// error shapes, and status vocabulary can all be exercised without a merchant account.
///
/// Retry, timeout, and circuit-breaker live in the HttpClient's resilience pipeline
/// (see CarrierExtensions), not here. This class handles only what is Pathao-specific:
/// bearer auth, the response envelope, and status translation.
/// </summary>
public sealed class PathaoAdapter(
    IHttpClientFactory httpClientFactory,
    IPathaoTokenProvider tokenProvider,
    TimeProvider timeProvider,
    ILogger<PathaoAdapter> logger) : ICarrierAdapter
{
    /// <summary>Named client configured with Pathao's base address and resilience pipeline.</summary>
    public const string HttpClientName = "pathao";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CarrierType Carrier => CarrierType.Pathao;

    public bool SupportsWebhooks => true;

    public async Task<CarrierTrackingResult?> GetStatusAsync(
        string trackingNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);

        var response = await SendWithAuthAsync(trackingNumber, retryOnUnauthorized: true, cancellationToken);

        // Pathao does not know this consignment. A normal answer, not a fault.
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            logger.LogInformation("Pathao has no record of consignment {TrackingNumber}", trackingNumber);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new CarrierApiException(
                "Pathao",
                $"order info for {trackingNumber} returned HTTP {(int)response.StatusCode}");
        }

        PathaoEnvelope<PathaoOrderInfo>? envelope;
        try
        {
            envelope = await response.Content.ReadFromJsonAsync<PathaoEnvelope<PathaoOrderInfo>>(
                JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new CarrierApiException("Pathao", $"unreadable order info for {trackingNumber}", ex);
        }

        // Most endpoints wrap the payload in { data: ... }; a few answer at the root.
        var info = envelope?.Data;
        if (info is null)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            info = TryDeserialiseRoot(body);
        }

        if (info is null)
            throw new CarrierApiException("Pathao", $"order info for {trackingNumber} contained no data");

        return ToResult(trackingNumber, info);
    }

    public CarrierTrackingResult? ParseWebhookPayload(string rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return null;

        PathaoOrderInfo? info;
        try
        {
            // Webhook bodies arrive unwrapped, unlike REST responses.
            info = JsonSerializer.Deserialize<PathaoOrderInfo>(rawPayload, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Discarding unparseable Pathao webhook payload");
            return null;
        }

        var trackingNumber = info?.ConsignmentId;
        if (info is null || string.IsNullOrWhiteSpace(trackingNumber))
        {
            logger.LogWarning("Discarding Pathao webhook payload with no consignment_id");
            return null;
        }

        return ToResult(trackingNumber, info);
    }

    /// <summary>
    /// Issues the request with a bearer token, retrying once on 401.
    ///
    /// A token can be revoked before its stated expiry, which the cache cannot predict.
    /// One retry after invalidating covers that without masking genuinely bad credentials —
    /// those fail twice and surface.
    /// </summary>
    private async Task<HttpResponseMessage> SendWithAuthAsync(
        string trackingNumber,
        bool retryOnUnauthorized,
        CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"aladdin/api/v1/orders/{Uri.EscapeDataString(trackingNumber)}/info");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new CarrierApiException("Pathao", $"request failed for {trackingNumber}", ex);
        }

        if (response.StatusCode is not HttpStatusCode.Unauthorized || !retryOnUnauthorized)
            return response;

        logger.LogWarning("Pathao rejected the cached token — re-authenticating and retrying once");
        response.Dispose();
        tokenProvider.Invalidate();

        return await SendWithAuthAsync(trackingNumber, retryOnUnauthorized: false, cancellationToken);
    }

    private static PathaoOrderInfo? TryDeserialiseRoot(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<PathaoOrderInfo>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private CarrierTrackingResult ToResult(string trackingNumber, PathaoOrderInfo info)
    {
        // Prefer the machine-readable slug; fall back to the display string.
        var raw = info.OrderStatusSlug ?? info.OrderStatus ?? string.Empty;
        var status = PathaoStatusMapper.ToCarrierStatus(raw);

        if (status is CarrierStatus.Unknown && !string.IsNullOrWhiteSpace(raw))
        {
            // Worth an alert in production: Pathao added a state we do not model yet.
            logger.LogWarning(
                "Unmapped Pathao status {RawStatus} for consignment {TrackingNumber}",
                raw, trackingNumber);
        }

        return new CarrierTrackingResult
        {
            TrackingNumber = trackingNumber,
            Status = status,
            RawStatus = raw,
            Description = info.OrderStatus ?? raw,
            Location = null,  // Pathao's order-info endpoint does not report hub location
            OccurredAt = ParseTimestamp(info.UpdatedAt),
            Carrier = CarrierType.Pathao
        };
    }

    /// <summary>
    /// Pathao sends "yyyy-MM-dd HH:mm:ss" without a zone. Times are Asia/Dhaka (UTC+6);
    /// converting here keeps everything downstream in UTC. An unparseable or missing
    /// timestamp falls back to now — a slightly wrong time beats dropping the update.
    /// </summary>
    private DateTime ParseTimestamp(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && DateTime.TryParse(value, out var parsed))
        {
            return parsed.Kind switch
            {
                DateTimeKind.Utc => parsed,
                DateTimeKind.Local => parsed.ToUniversalTime(),
                _ => DateTime.SpecifyKind(parsed.AddHours(-6), DateTimeKind.Utc)
            };
        }

        return timeProvider.GetUtcNow().UtcDateTime;
    }
}
