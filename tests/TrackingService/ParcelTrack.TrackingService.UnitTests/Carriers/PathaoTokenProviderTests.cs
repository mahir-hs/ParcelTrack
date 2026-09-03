using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using ParcelTrack.TrackingService.Domain.Exceptions;
using ParcelTrack.TrackingService.Infrastructure.Carriers.Pathao;

namespace ParcelTrack.TrackingService.UnitTests.Carriers;

public sealed class PathaoTokenProviderTests
{
    private const string TokenJson =
        """{"access_token":"tok_abc123","refresh_token":"ref_xyz","expires_in":3600,"token_type":"Bearer"}""";

    private static PathaoTokenProvider CreateProvider(
        StubHttpMessageHandler handler,
        FakeTimeProvider clock,
        int safetyMarginSeconds = 60)
    {
        var settings = Options.Create(new PathaoSettings
        {
            ClientId = "test-client",
            ClientSecret = "test-secret",
            Username = "test@pathao.com",
            Password = "lovePathao",
            TokenExpirySafetyMarginSeconds = safetyMarginSeconds
        });

        return new PathaoTokenProvider(
            new StubHttpClientFactory(handler),
            settings,
            clock,
            Mock.Of<ILogger<PathaoTokenProvider>>());
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldReturnTokenFromResponse()
    {
        var handler = new StubHttpMessageHandler().RespondWithJson(TokenJson);
        using var provider = CreateProvider(handler, new FakeTimeProvider());

        var token = await provider.GetAccessTokenAsync();

        token.Should().Be("tok_abc123");
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldPostToIssueTokenEndpoint()
    {
        var handler = new StubHttpMessageHandler().RespondWithJson(TokenJson);
        using var provider = CreateProvider(handler, new FakeTimeProvider());

        await provider.GetAccessTokenAsync();

        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Contain(PathaoTokenProvider.TokenEndpoint);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldCacheTokenAcrossCalls()
    {
        // The polling worker checks many consignments per cycle — one token must serve them all.
        var handler = new StubHttpMessageHandler().RespondWithJson(TokenJson);
        using var provider = CreateProvider(handler, new FakeTimeProvider());

        await provider.GetAccessTokenAsync();
        await provider.GetAccessTokenAsync();
        await provider.GetAccessTokenAsync();

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldRefreshAfterExpiry()
    {
        var handler = new StubHttpMessageHandler()
            .RespondWithJson(TokenJson)
            .RespondWithJson("""{"access_token":"tok_second","expires_in":3600}""");

        var clock = new FakeTimeProvider();
        using var provider = CreateProvider(handler, clock);

        var first = await provider.GetAccessTokenAsync();
        clock.Advance(TimeSpan.FromSeconds(3600));  // past expiry, given the 60s safety margin
        var second = await provider.GetAccessTokenAsync();

        first.Should().Be("tok_abc123");
        second.Should().Be("tok_second");
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldRefreshEarlyBySafetyMargin()
    {
        // A token expiring in 3600s is treated as dead at 3540s, so it never dies mid-request.
        var handler = new StubHttpMessageHandler()
            .RespondWithJson(TokenJson)
            .RespondWithJson("""{"access_token":"tok_second","expires_in":3600}""");

        var clock = new FakeTimeProvider();
        using var provider = CreateProvider(handler, clock, safetyMarginSeconds: 60);

        await provider.GetAccessTokenAsync();
        clock.Advance(TimeSpan.FromSeconds(3541));
        await provider.GetAccessTokenAsync();

        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldReuseTokenJustBeforeSafetyMargin()
    {
        var handler = new StubHttpMessageHandler().RespondWithJson(TokenJson);
        var clock = new FakeTimeProvider();
        using var provider = CreateProvider(handler, clock);

        await provider.GetAccessTokenAsync();
        clock.Advance(TimeSpan.FromSeconds(3000));
        await provider.GetAccessTokenAsync();

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Invalidate_ShouldForceReauthentication()
    {
        var handler = new StubHttpMessageHandler()
            .RespondWithJson(TokenJson)
            .RespondWithJson("""{"access_token":"tok_second","expires_in":3600}""");

        using var provider = CreateProvider(handler, new FakeTimeProvider());

        await provider.GetAccessTokenAsync();
        provider.Invalidate();
        var second = await provider.GetAccessTokenAsync();

        second.Should().Be("tok_second");
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldThrowWhenPathaoRejectsCredentials()
    {
        var handler = new StubHttpMessageHandler()
            .RespondWith(HttpStatusCode.Unauthorized, """{"message":"invalid credentials"}""");

        using var provider = CreateProvider(handler, new FakeTimeProvider());

        var act = async () => await provider.GetAccessTokenAsync();

        (await act.Should().ThrowAsync<CarrierApiException>())
            .Which.Carrier.Should().Be("Pathao");
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldThrowWhenResponseHasNoAccessToken()
    {
        var handler = new StubHttpMessageHandler().RespondWithJson("""{"expires_in":3600}""");
        using var provider = CreateProvider(handler, new FakeTimeProvider());

        var act = async () => await provider.GetAccessTokenAsync();

        await act.Should().ThrowAsync<CarrierApiException>()
            .WithMessage("*access_token*");
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldThrowWhenTransportFails()
    {
        var handler = new StubHttpMessageHandler().Throws(new HttpRequestException("connection reset"));
        using var provider = CreateProvider(handler, new FakeTimeProvider());

        var act = async () => await provider.GetAccessTokenAsync();

        await act.Should().ThrowAsync<CarrierApiException>();
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldIssueOneRequestUnderConcurrentCallers()
    {
        // A burst arriving on a cold cache must produce one token request, not one per caller.
        var handler = new StubHttpMessageHandler().RespondWithJson(TokenJson);
        using var provider = CreateProvider(handler, new FakeTimeProvider());

        var tokens = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => provider.GetAccessTokenAsync()));

        handler.CallCount.Should().Be(1);
        tokens.Should().AllBe("tok_abc123");
    }
}
