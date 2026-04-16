using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParcelTrack.ShipmentService.Application.DTOs;
using ParcelTrack.ShipmentService.Infrastructure.Persistence;
using ParcelTrack.ShipmentService.IntegrationTests.Auth;
using ParcelTrack.ShipmentService.IntegrationTests.Collections;
using ParcelTrack.ShipmentService.IntegrationTests.Fixtures;
using ParcelTrack.ShipmentService.IntegrationTests.Helpers;
using System.Net;
using System.Net.Http.Json;

namespace ParcelTrack.ShipmentService.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class ShipmentsControllerTests
{
    private readonly HttpClient _client;

    public ShipmentsControllerTests(PostgresContainerFixture postgres)
    {
        var factory = new ShipmentServiceFactory(postgres.ConnectionString);
        // ── Debug — remove once migrations are confirmed working ──────────────
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
        Console.WriteLine($"TEST DB:      {db.Database.GetConnectionString()}");
        Console.WriteLine($"CONTAINER DB: {postgres.ConnectionString}");
        // ─────────────────────────────────────────────────────────────────────
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateShipment_ValidRequest_Returns201AndPersists()
    {
        // Arrange
        var request = new
        {
            TrackingNumber = $"TRK-{Guid.NewGuid():N}",
            CarrierType = "Steadfast",
            DestinationCity = "Dhaka"
        };

        // Act
        var response = await _client
            .WithClaims(TestClaimsFactory.DefaultClaims())
            .PostAsJsonAsync("/v1/shipments", request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ShipmentDto>(TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.TrackingNumber.Should().Be(request.TrackingNumber);
    }

    [Fact]
    public async Task GetShipment_OwnTenant_Returns200()
    {
        // Arrange — create a shipment first
        var created = await CreateShipmentAsync(TestClaimsFactory.DefaultClaims());

        // Act
        var response = await _client
            .WithClaims(TestClaimsFactory.DefaultClaims())
            .GetAsync($"/v1/shipments/{created!.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetShipment_DifferentTenant_Returns404()
    {
        // Arrange — created by tenant A
        var created = await CreateShipmentAsync(TestClaimsFactory.DefaultClaims());

        // Act — requested by tenant B
        var response = await _client
            .WithClaims(TestClaimsFactory.AlternateClaims())
            .GetAsync($"/v1/shipments/{created!.Id}", TestContext.Current.CancellationToken);

        // Assert — global query filter hides it completely
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelShipment_CreatedStatus_Returns204()
    {
        // Arrange
        var created = await CreateShipmentAsync(TestClaimsFactory.DefaultClaims());
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/v1/shipments/{created!.Id}")
        {
            Content = JsonContent.Create(new { Reason = "Customer requested cancellation" })
        };

        var response = await _client
        .WithClaims(TestClaimsFactory.DefaultClaims())
        .SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateShipment_NoAuth_Returns401()
    {
        var request = new { TrackingNumber = "TRK-NOAUTH", CarrierType = "Steadfast" };

        var response = await _client.PostAsJsonAsync("/v1/shipments", request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<ShipmentDto?> CreateShipmentAsync(string claims)
    {
        var request = new
        {
            TrackingNumber = $"TRK-{Guid.NewGuid():N}",
            CarrierType = "Steadfast",
            DestinationCity = "Dhaka"
        };

        var response = await _client
            .WithClaims(claims)
            .PostAsJsonAsync("/v1/shipments", request);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShipmentDto>();
    }
}