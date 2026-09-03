namespace ParcelTrack.TrackingService.Infrastructure.Carriers.Pathao;

/// <summary>
/// Bound from the "Carriers:Pathao" configuration section.
///
/// Sandbox base URL is https://courier-api-sandbox.pathao.com and production is
/// https://api-hermes.pathao.com. Pathao publishes working sandbox credentials, so this
/// adapter is fully exercisable without a merchant account — but they are still credentials
/// in shape, so they live in configuration, never in source.
/// </summary>
public sealed class PathaoSettings
{
    public const string SectionName = "Carriers:Pathao";

    public string BaseUrl { get; set; } = "https://courier-api-sandbox.pathao.com";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Refresh this many seconds before the token actually expires, to avoid racing the clock.</summary>
    public int TokenExpirySafetyMarginSeconds { get; set; } = 60;

    /// <summary>Per-request timeout. Pathao is usually fast; a slow call is a failing call.</summary>
    public int TimeoutSeconds { get; set; } = 10;
}
