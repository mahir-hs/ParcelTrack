using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using ParcelTrack.TrackingService.Domain.Enums;
using ParcelTrack.TrackingService.Domain.Exceptions;
using ParcelTrack.TrackingService.Infrastructure.Carriers.Pathao;

namespace ParcelTrack.TrackingService.UnitTests.Carriers;

public sealed class PathaoAdapterTests
{
    private const string ConsignmentId = "DA240101ABCDE";

    private static string OrderInfoJson(
        string status = "Delivered",
        string slug = "Delivered",
        string updatedAt = "2026-03-09 15:30:00") =>
        $$"""
        {
          "type": "success",
          "code": 200,
          "message": "Order info",
          "data": {
            "consignment_id": "{{ConsignmentId}}",
            "merchant_order_id": "ORD-1",
            "order_status": "{{status}}",
            "order_status_slug": "{{slug}}",
            "updated_at": "{{updatedAt}}",
            "invoice_id": null
          }
        }
        """;

    private static PathaoAdapter CreateAdapter(
        StubHttpMessageHandler handler,
        FakeTimeProvider? clock = null,
        IPathaoTokenProvider? tokenProvider = null)
    {
        var tokens = tokenProvider ?? StubTokenProvider();

        return new PathaoAdapter(
            new StubHttpClientFactory(handler),
            tokens,
            clock ?? new FakeTimeProvider(),
            Mock.Of<ILogger<PathaoAdapter>>());
    }

    private static IPathaoTokenProvider StubTokenProvider(string token = "tok_abc123")
    {
        var mock = new Mock<IPathaoTokenProvider>();
        mock.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        return mock.Object;
    }

    // ── Identity ────────────────────────────────────────────────

    [Fact]
    public void Carrier_ShouldBePathao()
    {
        CreateAdapter(new StubHttpMessageHandler()).Carrier.Should().Be(CarrierType.Pathao);
    }

    [Fact]
    public void SupportsWebhooks_ShouldBeTrue()
    {
        CreateAdapter(new StubHttpMessageHandler()).SupportsWebhooks.Should().BeTrue();
    }

    // ── Happy path ──────────────────────────────────────────────

    [Fact]
    public async Task GetStatusAsync_ShouldMapDeliveredStatus()
    {
        var adapter = CreateAdapter(new StubHttpMessageHandler().RespondWithJson(OrderInfoJson()));

        var result = await adapter.GetStatusAsync(ConsignmentId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(CarrierStatus.Delivered);
        result.TrackingNumber.Should().Be(ConsignmentId);
        result.Carrier.Should().Be(CarrierType.Pathao);
    }

    [Fact]
    public async Task GetStatusAsync_ShouldPreserveRawCarrierStatus()
    {
        // The courier's own wording is kept for audit and for diagnosing unmapped values.
        var adapter = CreateAdapter(new StubHttpMessageHandler()
            .RespondWithJson(OrderInfoJson(status: "Assigned for Delivery", slug: "Assigned_for_Delivery")));

        var result = await adapter.GetStatusAsync(ConsignmentId);

        result!.RawStatus.Should().Be("Assigned_for_Delivery");
        result.Status.Should().Be(CarrierStatus.OutForDelivery);
        result.Description.Should().Be("Assigned for Delivery");
    }

    [Fact]
    public async Task GetStatusAsync_ShouldSendBearerToken()
    {
        var handler = new StubHttpMessageHandler().RespondWithJson(OrderInfoJson());
        var adapter = CreateAdapter(handler);

        await adapter.GetStatusAsync(ConsignmentId);

        var auth = handler.Requests[0].Headers.Authorization;
        auth!.Scheme.Should().Be("Bearer");
        auth.Parameter.Should().Be("tok_abc123");
    }

    [Fact]
    public async Task GetStatusAsync_ShouldRequestTheOrderInfoEndpoint()
    {
        var handler = new StubHttpMessageHandler().RespondWithJson(OrderInfoJson());
        var adapter = CreateAdapter(handler);

        await adapter.GetStatusAsync(ConsignmentId);

        handler.Requests[0].RequestUri!.AbsolutePath
            .Should().Be($"/aladdin/api/v1/orders/{ConsignmentId}/info");
    }

    [Fact]
    public async Task GetStatusAsync_ShouldConvertDhakaTimeToUtc()
    {
        // Pathao sends local time without a zone. Asia/Dhaka is UTC+6.
        var adapter = CreateAdapter(new StubHttpMessageHandler()
            .RespondWithJson(OrderInfoJson(updatedAt: "2026-03-09 15:30:00")));

        var result = await adapter.GetStatusAsync(ConsignmentId);

        result!.OccurredAt.Should().Be(new DateTime(2026, 3, 9, 9, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetStatusAsync_ShouldFallBackToNowWhenTimestampMissing()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        var adapter = CreateAdapter(
            new StubHttpMessageHandler().RespondWithJson(OrderInfoJson(updatedAt: "")),
            clock);

        var result = await adapter.GetStatusAsync(ConsignmentId);

        result!.OccurredAt.Should().Be(new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetStatusAsync_ShouldReadPayloadServedWithoutEnvelope()
    {
        // Not every Pathao endpoint wraps its payload in { data: ... }.
        var adapter = CreateAdapter(new StubHttpMessageHandler().RespondWithJson(
            $$"""{"consignment_id":"{{ConsignmentId}}","order_status":"Delivered","order_status_slug":"Delivered"}"""));

        var result = await adapter.GetStatusAsync(ConsignmentId);

        result!.Status.Should().Be(CarrierStatus.Delivered);
    }

    [Fact]
    public async Task GetStatusAsync_ShouldReturnUnknownForUnmappedStatusWithoutThrowing()
    {
        var adapter = CreateAdapter(new StubHttpMessageHandler()
            .RespondWithJson(OrderInfoJson(status: "Teleported", slug: "Teleported")));

        var result = await adapter.GetStatusAsync(ConsignmentId);

        result!.Status.Should().Be(CarrierStatus.Unknown);
        result.RawStatus.Should().Be("Teleported");
    }

    // ── Failure handling ────────────────────────────────────────

    [Fact]
    public async Task GetStatusAsync_ShouldReturnNullWhenConsignmentUnknown()
    {
        // "No such parcel" is an answer, not a fault — it must not trip retries or breakers.
        var adapter = CreateAdapter(new StubHttpMessageHandler()
            .RespondWith(HttpStatusCode.NotFound, """{"message":"not found"}"""));

        var result = await adapter.GetStatusAsync(ConsignmentId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStatusAsync_ShouldThrowOnServerError()
    {
        var adapter = CreateAdapter(new StubHttpMessageHandler()
            .RespondWith(HttpStatusCode.InternalServerError, "{}"));

        var act = async () => await adapter.GetStatusAsync(ConsignmentId);

        await act.Should().ThrowAsync<CarrierApiException>();
    }

    [Fact]
    public async Task GetStatusAsync_ShouldThrowOnMalformedJson()
    {
        var adapter = CreateAdapter(new StubHttpMessageHandler().RespondWithJson("{ not json"));

        var act = async () => await adapter.GetStatusAsync(ConsignmentId);

        await act.Should().ThrowAsync<CarrierApiException>();
    }

    [Fact]
    public async Task GetStatusAsync_ShouldThrowWhenTransportFails()
    {
        var adapter = CreateAdapter(new StubHttpMessageHandler()
            .Throws(new HttpRequestException("connection reset")));

        var act = async () => await adapter.GetStatusAsync(ConsignmentId);

        await act.Should().ThrowAsync<CarrierApiException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetStatusAsync_ShouldRejectBlankTrackingNumber(string? trackingNumber)
    {
        var adapter = CreateAdapter(new StubHttpMessageHandler());

        var act = async () => await adapter.GetStatusAsync(trackingNumber!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Token expiry mid-flight ─────────────────────────────────

    [Fact]
    public async Task GetStatusAsync_ShouldRetryOnceAfterUnauthorized()
    {
        // A token can be revoked before its stated expiry; one re-auth covers that.
        var handler = new StubHttpMessageHandler()
            .RespondWith(HttpStatusCode.Unauthorized, "{}")
            .RespondWithJson(OrderInfoJson());

        var tokens = new Mock<IPathaoTokenProvider>();
        tokens.Setup(t => t.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("tok");

        var result = await CreateAdapter(handler, tokenProvider: tokens.Object).GetStatusAsync(ConsignmentId);

        result!.Status.Should().Be(CarrierStatus.Delivered);
        handler.CallCount.Should().Be(2);
        tokens.Verify(t => t.Invalidate(), Times.Once);
    }

    [Fact]
    public async Task GetStatusAsync_ShouldNotRetryUnauthorizedTwice()
    {
        // Genuinely bad credentials must surface rather than loop.
        var handler = new StubHttpMessageHandler()
            .RespondWith(HttpStatusCode.Unauthorized, "{}")
            .RespondWith(HttpStatusCode.Unauthorized, "{}");

        var act = async () => await CreateAdapter(handler).GetStatusAsync(ConsignmentId);

        await act.Should().ThrowAsync<CarrierApiException>();
        handler.CallCount.Should().Be(2);
    }

    // ── Webhook parsing ─────────────────────────────────────────

    [Fact]
    public void ParseWebhookPayload_ShouldMapValidPayload()
    {
        var adapter = CreateAdapter(new StubHttpMessageHandler());

        var result = adapter.ParseWebhookPayload(
            $$"""{"consignment_id":"{{ConsignmentId}}","order_status":"In Transit","order_status_slug":"In_Transit","updated_at":"2026-03-09 15:30:00"}""");

        result.Should().NotBeNull();
        result!.Status.Should().Be(CarrierStatus.InTransit);
        result.TrackingNumber.Should().Be(ConsignmentId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    public void ParseWebhookPayload_ShouldReturnNullForUnusablePayload(string payload)
    {
        CreateAdapter(new StubHttpMessageHandler()).ParseWebhookPayload(payload).Should().BeNull();
    }

    [Fact]
    public void ParseWebhookPayload_ShouldReturnNullWhenConsignmentIdMissing()
    {
        var adapter = CreateAdapter(new StubHttpMessageHandler());

        adapter.ParseWebhookPayload("""{"order_status_slug":"Delivered"}""").Should().BeNull();
    }
}
