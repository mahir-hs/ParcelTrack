using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.WebhookDispatchService.Worker.Application;
using ParcelTrack.WebhookDispatchService.Worker.Domain;
using ParcelTrack.WebhookDispatchService.Worker.Infrastructure;

namespace ParcelTrack.WebhookDispatchService.UnitTests.Application;

public sealed class WebhookDispatchHandlerTests
{
    private readonly Mock<IWebhookSubscriptionRepository> _subscriptionsMock;
    private readonly Mock<IWebhookDeliveryRepository> _deliveriesMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly WebhookDispatchHandler _handler;

    public WebhookDispatchHandlerTests()
    {
        _subscriptionsMock = new Mock<IWebhookSubscriptionRepository>();
        _deliveriesMock = new Mock<IWebhookDeliveryRepository>();

        _deliveriesMock
            .Setup(d => d.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _deliveriesMock
            .Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _httpHandlerMock = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_httpHandlerMock.Object);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(f => f.CreateClient("webhook"))
            .Returns(httpClient);

        _handler = new WebhookDispatchHandler(
            _subscriptionsMock.Object,
            _deliveriesMock.Object,
            httpClientFactoryMock.Object,
            Mock.Of<ILogger<WebhookDispatchHandler>>());
    }

    // ── No subscriptions ──────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WithNoActiveSubscriptions_ShouldNotCreateDelivery()
    {
        _subscriptionsMock
            .Setup(s => s.GetActiveByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WebhookSubscription>());

        await _handler.HandleAsync(BuildEvent());

        _deliveriesMock.Verify(
            d => d.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithNoActiveSubscriptions_ShouldNotMakeHttpCall()
    {
        _subscriptionsMock
            .Setup(s => s.GetActiveByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WebhookSubscription>());

        await _handler.HandleAsync(BuildEvent());

        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    // ── Successful dispatch ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WithActiveSubscription_ShouldCreateDelivery()
    {
        SetupSubscriptions(BuildSubscription());
        SetupHttpResponse(HttpStatusCode.OK);

        await _handler.HandleAsync(BuildEvent());

        _deliveriesMock.Verify(
            d => d.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithActiveSubscription_ShouldMakePostRequest()
    {
        SetupSubscriptions(BuildSubscription(targetUrl: "https://example.com/hook"));
        SetupHttpResponse(HttpStatusCode.OK);

        await _handler.HandleAsync(BuildEvent());

        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.Method == HttpMethod.Post &&
                r.RequestUri == new Uri("https://example.com/hook")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithTwoSubscriptions_ShouldCreateTwoDeliveries()
    {
        SetupSubscriptions(BuildSubscription(), BuildSubscription());
        SetupHttpResponse(HttpStatusCode.OK);

        await _handler.HandleAsync(BuildEvent());

        _deliveriesMock.Verify(
            d => d.AddAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ── Signature header ──────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WithSubscriptionSecret_ShouldAddSignatureHeader()
    {
        SetupSubscriptions(BuildSubscription(secret: "mysecret"));
        SetupHttpResponse(HttpStatusCode.OK);

        await _handler.HandleAsync(BuildEvent());

        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.Headers.Contains("X-ParcelTrack-Signature")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithNoSecret_ShouldNotAddSignatureHeader()
    {
        SetupSubscriptions(BuildSubscription(secret: null));
        SetupHttpResponse(HttpStatusCode.OK);

        await _handler.HandleAsync(BuildEvent());

        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                !r.Headers.Contains("X-ParcelTrack-Signature")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SignatureHeader_ShouldUseSha256Prefix()
    {
        SetupSubscriptions(BuildSubscription(secret: "mysecret"));
        SetupHttpResponse(HttpStatusCode.OK);

        await _handler.HandleAsync(BuildEvent());

        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.Headers.GetValues("X-ParcelTrack-Signature").First().StartsWith("sha256=")),
            ItExpr.IsAny<CancellationToken>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupSubscriptions(params WebhookSubscription[] subs) =>
        _subscriptionsMock
            .Setup(s => s.GetActiveByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subs.ToList());

    private void SetupHttpResponse(HttpStatusCode statusCode) =>
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));

    private static WebhookSubscription BuildSubscription(
        string targetUrl = "https://example.com/hook",
        string? secret = "s3cr3t") =>
        WebhookSubscription.Create(Guid.NewGuid(), targetUrl, secret);

    private static ShipmentStatusChangedEvent BuildEvent() =>
        new(
            ShipmentId: Guid.NewGuid(),
            TrackingNumber: "STD-001",
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            BuyerEmail: null,
            PreviousStatus: "InTransit",
            NewStatus: "Delivered",
            Location: "Dhaka",
            Description: "Delivered",
            OccurredAt: DateTime.UtcNow);
}
