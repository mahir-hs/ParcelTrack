namespace ParcelTrack.ShipmentService.Application.Interfaces;

/// <summary>
/// Sets the tenant for work that has no HTTP request behind it.
///
/// <see cref="ITenantContext"/> normally reads JWT claims, which is right for API calls but
/// leaves background consumers unable to do anything at all: the tenant filter on every query
/// needs a TenantId, and there is no token to take one from. A Kafka message carries its own
/// TenantId, so the consumer states it explicitly before invoking a handler.
///
/// Deliberately a separate interface from ITenantContext. Handlers depend on the read side and
/// cannot reassign the tenant mid-request even by accident — only infrastructure that owns a
/// scope's lifetime takes this dependency.
/// </summary>
public interface ITenantContextSetter
{
    /// <summary>
    /// Pins this scope to a tenant. Must be called before any tenant-scoped query runs,
    /// and only on a scope created for background work.
    /// </summary>
    void SetContext(Guid tenantId, Guid userId);
}
