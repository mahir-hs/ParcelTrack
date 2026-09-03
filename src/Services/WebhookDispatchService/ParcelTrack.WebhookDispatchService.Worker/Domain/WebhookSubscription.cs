namespace ParcelTrack.WebhookDispatchService.Worker.Domain;

public sealed class WebhookSubscription
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string TargetUrl { get; private set; } = string.Empty;
    public string? Secret { get; private set; }  // used to sign payloads (HMAC-SHA256)
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private WebhookSubscription() { }

    public static WebhookSubscription Create(Guid tenantId, string targetUrl, string? secret = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        TargetUrl = targetUrl,
        Secret = secret,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    public void Deactivate() => IsActive = false;
}
