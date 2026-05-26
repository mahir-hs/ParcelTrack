using FluentAssertions;
using ParcelTrack.WebhookDispatchService.Worker.Domain;

namespace ParcelTrack.WebhookDispatchService.UnitTests.Domain;

public sealed class WebhookDeliveryTests
{
    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldAssignNewId()
    {
        var delivery = BuildDelivery();

        delivery.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_TwoDeliveries_ShouldHaveDifferentIds()
    {
        var first = BuildDelivery();
        var second = BuildDelivery();

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Create_ShouldInitialiseAttemptCountToZero()
    {
        var delivery = BuildDelivery();

        delivery.AttemptCount.Should().Be(0);
    }

    [Fact]
    public void Create_ShouldInitialiseAsNotSuccessful()
    {
        var delivery = BuildDelivery();

        delivery.IsSuccessful.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldStoreEventTypeAndPayload()
    {
        var subscriptionId = Guid.NewGuid();
        var delivery = WebhookDelivery.Create(subscriptionId, "shipment.status.changed", "{\"key\":\"val\"}");

        delivery.SubscriptionId.Should().Be(subscriptionId);
        delivery.EventType.Should().Be("shipment.status.changed");
        delivery.Payload.Should().Be("{\"key\":\"val\"}");
    }

    [Fact]
    public void Create_ShouldNotBeExhausted()
    {
        var delivery = BuildDelivery();

        delivery.IsExhausted.Should().BeFalse();
    }

    // ── RecordSuccess ─────────────────────────────────────────────────────────

    [Fact]
    public void RecordSuccess_ShouldMarkAsSuccessful()
    {
        var delivery = BuildDelivery();

        delivery.RecordSuccess(200);

        delivery.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void RecordSuccess_ShouldIncrementAttemptCount()
    {
        var delivery = BuildDelivery();

        delivery.RecordSuccess(200);

        delivery.AttemptCount.Should().Be(1);
    }

    [Fact]
    public void RecordSuccess_ShouldStoreResponseStatusCode()
    {
        var delivery = BuildDelivery();

        delivery.RecordSuccess(201);

        delivery.ResponseStatusCode.Should().Be(201);
    }

    [Fact]
    public void RecordSuccess_ShouldSetDeliveredAt()
    {
        var delivery = BuildDelivery();

        delivery.RecordSuccess(200);

        delivery.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordSuccess_ShouldNotBeExhausted()
    {
        var delivery = BuildDelivery();

        delivery.RecordSuccess(200);

        delivery.IsExhausted.Should().BeFalse();
    }

    // ── RecordFailure ─────────────────────────────────────────────────────────

    [Fact]
    public void RecordFailure_ShouldIncrementAttemptCount()
    {
        var delivery = BuildDelivery();

        delivery.RecordFailure(503, "Service unavailable");

        delivery.AttemptCount.Should().Be(1);
    }

    [Fact]
    public void RecordFailure_ShouldStoreErrorMessage()
    {
        var delivery = BuildDelivery();

        delivery.RecordFailure(500, "Internal server error");

        delivery.ErrorMessage.Should().Be("Internal server error");
    }

    [Fact]
    public void RecordFailure_ShouldStoreNullableStatusCode()
    {
        var delivery = BuildDelivery();

        delivery.RecordFailure(null, "Connection refused");

        delivery.ResponseStatusCode.Should().BeNull();
    }

    [Fact]
    public void RecordFailure_ShouldNotMarkAsSuccessful()
    {
        var delivery = BuildDelivery();

        delivery.RecordFailure(500, "Error");

        delivery.IsSuccessful.Should().BeFalse();
    }

    // ── IsExhausted ───────────────────────────────────────────────────────────

    [Fact]
    public void IsExhausted_AfterMaxAttemptsAllFailed_ShouldBeTrue()
    {
        var delivery = BuildDelivery();

        for (var i = 0; i < WebhookDelivery.MaxAttempts; i++)
            delivery.RecordFailure(500, "Error");

        delivery.IsExhausted.Should().BeTrue();
    }

    [Fact]
    public void IsExhausted_BeforeMaxAttempts_ShouldBeFalse()
    {
        var delivery = BuildDelivery();

        delivery.RecordFailure(500, "Error"); // only 1 of 3

        delivery.IsExhausted.Should().BeFalse();
    }

    [Fact]
    public void IsExhausted_AfterSuccessOnLastAttempt_ShouldBeFalse()
    {
        var delivery = BuildDelivery();
        delivery.RecordFailure(500, "Error");
        delivery.RecordFailure(500, "Error");

        delivery.RecordSuccess(200); // succeeds on attempt 3

        delivery.IsExhausted.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WebhookDelivery BuildDelivery() =>
        WebhookDelivery.Create(Guid.NewGuid(), "shipment.status.changed", "{}");
}
