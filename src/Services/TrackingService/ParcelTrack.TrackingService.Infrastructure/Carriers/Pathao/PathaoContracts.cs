using System.Text.Json.Serialization;

namespace ParcelTrack.TrackingService.Infrastructure.Carriers.Pathao;

/// <summary>Request body for POST /aladdin/api/v1/issue-token.</summary>
internal sealed record PathaoTokenRequest(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_secret")] string ClientSecret,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("grant_type")] string GrantType = "password");

/// <summary>Token response. Pathao returns these at the document root.</summary>
internal sealed record PathaoTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }
}

/// <summary>
/// Pathao wraps most successful payloads as { type, code, message, data }.
/// Not every endpoint does, so the adapter falls back to reading the root when Data is absent.
/// </summary>
internal sealed record PathaoEnvelope<T>
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }
}

/// <summary>Consignment status from GET /aladdin/api/v1/orders/{consignmentId}/info.</summary>
internal sealed record PathaoOrderInfo
{
    [JsonPropertyName("consignment_id")]
    public string? ConsignmentId { get; init; }

    [JsonPropertyName("merchant_order_id")]
    public string? MerchantOrderId { get; init; }

    [JsonPropertyName("order_status")]
    public string? OrderStatus { get; init; }

    [JsonPropertyName("order_status_slug")]
    public string? OrderStatusSlug { get; init; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }

    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; init; }
}
