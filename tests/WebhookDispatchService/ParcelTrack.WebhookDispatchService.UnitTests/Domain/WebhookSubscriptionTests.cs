using FluentAssertions;
using ParcelTrack.WebhookDispatchService.Worker.Domain;

namespace ParcelTrack.WebhookDispatchService.UnitTests.Domain;

public sealed class WebhookSubscriptionTests
{
    [Fact]
    public void Create_ShouldAssignNewId()
    {
        var sub = BuildSubscription();

        sub.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_TwoSubscriptions_ShouldHaveDifferentIds()
    {
        var first = BuildSubscription();
        var second = BuildSubscription();

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Create_ShouldBeActiveByDefault()
    {
        var sub = BuildSubscription();

        sub.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldStoreTargetUrl()
    {
        var sub = WebhookSubscription.Create(Guid.NewGuid(), "https://example.com/webhook");

        sub.TargetUrl.Should().Be("https://example.com/webhook");
    }

    [Fact]
    public void Create_ShouldStoreTenantId()
    {
        var tenantId = Guid.NewGuid();
        var sub = WebhookSubscription.Create(tenantId, "https://example.com/webhook");

        sub.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void Create_WithSecret_ShouldStoreSecret()
    {
        var sub = WebhookSubscription.Create(Guid.NewGuid(), "https://example.com/webhook", secret: "s3cr3t");

        sub.Secret.Should().Be("s3cr3t");
    }

    [Fact]
    public void Create_WithoutSecret_ShouldHaveNullSecret()
    {
        var sub = WebhookSubscription.Create(Guid.NewGuid(), "https://example.com/webhook");

        sub.Secret.Should().BeNull();
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var sub = BuildSubscription();

        sub.Deactivate();

        sub.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_CalledTwice_ShouldRemainInactive()
    {
        var sub = BuildSubscription();
        sub.Deactivate();

        sub.Deactivate();

        sub.IsActive.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WebhookSubscription BuildSubscription() =>
        WebhookSubscription.Create(Guid.NewGuid(), "https://example.com/webhook", secret: "s3cr3t");
}
