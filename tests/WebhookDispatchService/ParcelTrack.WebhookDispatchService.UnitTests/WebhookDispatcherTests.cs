using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ParcelTrack.Shared.Contracts;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.Shared.Messaging;
using ParcelTrack.WebhookDispatchService.Worker;
using ParcelTrack.WebhookDispatchService.Worker.Models;

namespace ParcelTrack.WebhookDispatchService.UnitTests;

public sealed class WebhookDispatcherTests
{
    // NOTE: WebhookDispatcher uses a hardcoded `MaxAttempts = 3` constant for the
    // number of POST attempts, so a persistently failing subscriber is always hit
    // exactly 3 times before being dead-lettered. These tests assert that real
    // behaviour rather than a (non-existent) configurable retry count.
    private const int ExpectedAttempts = 3;

    private readonly FakeHttpMessageHandler _handler;
    private readonly Mock<IKafkaProducer> _producerMock;
    private readonly Mock<ILogger<WebhookDispatcher>> _loggerMock;
    private readonly WebhookDispatcher _dispatcher;

    public WebhookDispatcherTests()
    {
        _handler = new FakeHttpMessageHandler(HttpStatusCode.OK);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        // IHttpClientFactory hands out a *new* HttpClient per call (the real factory
        // shares the handler pool but returns distinct client instances). This matters
        // because WebhookDispatcher disposes the client after each subscription.
        httpClientFactoryMock
            .Setup(f => f.CreateClient("webhook"))
            .Returns(() => new HttpClient(_handler) { BaseAddress = new Uri("https://localhost/") });

        _producerMock = new Mock<IKafkaProducer>();
        _producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _loggerMock = new Mock<ILogger<WebhookDispatcher>>();

        var options = new WebhookOptions
        {
            Subscriptions =
            [
                new WebhookOptions.Subscription
                {
                    Name = "Acme",
                    Url = "https://acme.test/webhook",
                    Events = ["*"]
                }
            ]
        };

        var optionsMock = new Mock<IOptions<WebhookOptions>>();
        optionsMock.Setup(o => o.Value).Returns(options);

        _dispatcher = new WebhookDispatcher(
            httpClientFactoryMock.Object,
            _producerMock.Object,
            optionsMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task DispatchAsync_WhenSubscriberReturns2xx_DeliversWithoutRetryOrDeadLetter()
    {
        // Arrange
        _handler.ResetTo(HttpStatusCode.OK);
        var payload = BuildPayload();

        // Act
        await _dispatcher.DispatchAsync(Topics.ShipmentStatusChanged, payload, CancellationToken.None);

        // Assert
        _handler.SendCount.Should().Be(1, "a 2xx response should not trigger a retry");
        _producerMock.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "a successful delivery must not be dead-lettered");
    }

    [Fact]
    public async Task DispatchAsync_WhenSubscriberReturns5xx_RetriesThenDeadLetters()
    {
        // Arrange
        _handler.ResetTo(HttpStatusCode.InternalServerError);
        var payload = BuildPayload();

        // Act
        await _dispatcher.DispatchAsync(Topics.ShipmentStatusChanged, payload, CancellationToken.None);

        // Assert — retried up to MaxAttempts
        _handler.SendCount.Should().Be(ExpectedAttempts);

        // Assert — dead-letter published on the webhook.failed topic
        _producerMock.Verify(
            p => p.ProduceAsync(
                Topics.WebhookFailed,
                typeof(WebhookFailedEvent).FullName!,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenHttpClientThrows_RetriesThenDeadLetters()
    {
        // Arrange — simulate a transport-level failure (DNS, connection refused, timeout)
        var throwingHandler = new FakeHttpMessageHandler(new HttpRequestException("connection refused"));

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(f => f.CreateClient("webhook"))
            .Returns(() => new HttpClient(throwingHandler) { BaseAddress = new Uri("https://localhost/") });

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var options = BuildOptions("Acme", "https://acme.test/webhook", ["*"]);
        var optionsMock = new Mock<IOptions<WebhookOptions>>();
        optionsMock.Setup(o => o.Value).Returns(options);

        var dispatcher = new WebhookDispatcher(
            httpClientFactoryMock.Object,
            producerMock.Object,
            optionsMock.Object,
            new Mock<ILogger<WebhookDispatcher>>().Object);

        var payload = BuildPayload();

        // Act
        await dispatcher.DispatchAsync(Topics.ShipmentStatusChanged, payload, CancellationToken.None);

        // Assert
        throwingHandler.SendCount.Should().Be(ExpectedAttempts);
        producerMock.Verify(
            p => p.ProduceAsync(
                Topics.WebhookFailed,
                typeof(WebhookFailedEvent).FullName!,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_AttemptsMatchMaxAttempts_ForPersistent5xx()
    {
        // Arrange
        _handler.ResetTo(HttpStatusCode.BadGateway);
        var payload = BuildPayload();

        // Act
        await _dispatcher.DispatchAsync(Topics.ShipmentStatusChanged, payload, CancellationToken.None);

        // Assert — exactly MaxAttempts POSTs, no more, no less
        _handler.SendCount.Should().Be(ExpectedAttempts);
        _handler.Requests.Should().HaveCount(ExpectedAttempts);
    }

    [Fact]
    public async Task DispatchAsync_DeadLetterPayloadCarriesAttemptsAndSubscriptionContext()
    {
        // Arrange
        _handler.ResetTo(HttpStatusCode.ServiceUnavailable);
        var payload = BuildPayload();

        string? capturedPayload = null;
        _producerMock
            .Setup(p => p.ProduceAsync(
                Topics.WebhookFailed,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, p, _) => capturedPayload = p)
            .Returns(Task.CompletedTask);

        // Act
        await _dispatcher.DispatchAsync(Topics.ShipmentStatusChanged, payload, CancellationToken.None);

        // Assert
        capturedPayload.Should().NotBeNull();
        var failed = JsonSerializer.Deserialize<WebhookFailedEvent>(capturedPayload!);
        failed.Should().NotBeNull();
        failed!.Attempts.Should().Be(ExpectedAttempts);
        failed.SubscriptionName.Should().Be("Acme");
        failed.TargetUrl.Should().Be("https://acme.test/webhook");
        failed.EventType.Should().Be(Topics.ShipmentStatusChanged);
    }

    [Fact]
    public async Task DispatchAsync_WhenNoSubscriptionMatchesTopic_SkipsHttpAndDeadLetter()
    {
        // Arrange — subscription only listens to a different topic
        var options = BuildOptions("Other", "https://other.test/webhook", ["shipment.created"]);
        var optionsMock = new Mock<IOptions<WebhookOptions>>();
        optionsMock.Setup(o => o.Value).Returns(options);

        var unreachedHandler = new FakeHttpMessageHandler(HttpStatusCode.OK);

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(f => f.CreateClient("webhook"))
            .Returns(() => new HttpClient(unreachedHandler) { BaseAddress = new Uri("https://localhost/") });

        var producerMock = new Mock<IKafkaProducer>();
        var dispatcher = new WebhookDispatcher(
            httpClientFactoryMock.Object,
            producerMock.Object,
            optionsMock.Object,
            new Mock<ILogger<WebhookDispatcher>>().Object);

        var payload = BuildPayload();

        // Act
        await dispatcher.DispatchAsync(Topics.ShipmentStatusChanged, payload, CancellationToken.None);

        // Assert
        unreachedHandler.SendCount.Should().Be(0, "non-matching subscriptions must not be called");
        producerMock.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_MixedSubscriptions_DeadLettersOnlyTheFailures()
    {
        // Arrange — one good subscriber ("ok") and one bad subscriber ("bad") so we
        // can confirm dead-lettering happens per-subscription.
        var combinedHandler = new RoutingFakeHandler(okUrl: "https://ok.test/hook");

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock
            .Setup(f => f.CreateClient("webhook"))
            .Returns(() => new HttpClient(combinedHandler) { BaseAddress = new Uri("https://localhost/") });

        var options = new WebhookOptions
        {
            Subscriptions =
            [
                new WebhookOptions.Subscription { Name = "ok", Url = "https://ok.test/hook", Events = ["*"] },
                new WebhookOptions.Subscription { Name = "bad", Url = "https://bad.test/hook", Events = ["*"] }
            ]
        };
        var optionsMock = new Mock<IOptions<WebhookOptions>>();
        optionsMock.Setup(o => o.Value).Returns(options);

        var producerMock = new Mock<IKafkaProducer>();
        producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dispatcher = new WebhookDispatcher(
            factoryMock.Object,
            producerMock.Object,
            optionsMock.Object,
            new Mock<ILogger<WebhookDispatcher>>().Object);

        var payload = BuildPayload();

        // Act
        await dispatcher.DispatchAsync(Topics.ShipmentStatusChanged, payload, CancellationToken.None);

        // Assert — the good subscriber was called once and never dead-lettered;
        // the bad subscriber was attempted 3 times and dead-lettered once.
        combinedHandler.SeenUris.Should().Contain("https://bad.test/hook");
        combinedHandler.OkSendCount.Should().Be(1);
        combinedHandler.BadSendCount.Should().Be(ExpectedAttempts);

        producerMock.Verify(
            p => p.ProduceAsync(
                Topics.WebhookFailed,
                typeof(WebhookFailedEvent).FullName!,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once, "only the failing subscriber should be dead-lettered");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static WebhookOptions BuildOptions(string name, string url, List<string> events) => new()
    {
        Subscriptions =
        [
            new WebhookOptions.Subscription { Name = name, Url = url, Events = events }
        ]
    };

    private static ShipmentStatusChangedEvent BuildPayload() => new(
        ShipmentId: Guid.NewGuid(),
        TrackingNumber: "STD-TEST-001",
        TenantId: Guid.NewGuid(),
        UserId: Guid.NewGuid(),
        PreviousStatus: "InTransit",
        NewStatus: "OutForDelivery",
        Location: "Dhaka",
        Description: "Out for delivery",
        OccurredAt: DateTime.UtcNow);
}
