using FluentAssertions;
using ParcelTrack.ShipmentService.IntegrationTests.Auth;
using ParcelTrack.ShipmentService.IntegrationTests.Collections;
using ParcelTrack.ShipmentService.IntegrationTests.Fixtures;
using ParcelTrack.ShipmentService.IntegrationTests.Helpers;
using System.Net;
using System.Net.Http.Json;

namespace ParcelTrack.ShipmentService.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class TrackingControllerTests : IClassFixture<ShipmentServiceFactory>
{
    private readonly HttpClient _client;

    public TrackingControllerTests(PostgresContainerFixture postgres)
    {
        var factory = new ShipmentServiceFactory(postgres.ConnectionString);
        _client = factory.CreateClient();
    }

    //[Fact]
    //public async Task Track_KnownTrackingNumber_Returns200WithEvents()
    //{
    //    // Arrange — create a shipment so the tracking number exists
    //    var trackingNumber = $"TRK-{Guid.NewGuid():N}";
    //    await CreateShipmentAsync(trackingNumber);

    //    // Act — no auth header, public endpoint
    //    var response = await _client.GetAsync($"/track/{trackingNumber}", TestContext.Current.CancellationToken);

    //    // Assert
    //    response.StatusCode.Should().Be(HttpStatusCode.OK);

    //    var body = await response.Content.ReadFromJsonAsync<TrackingResponseDto>(TestContext.Current.CancellationToken);
    //    body.Should().NotBeNull(TestContext.Current.CancellationToken);
    //    body!.TrackingNumber.Should(TestContext.Current.CancellationToken).Be(trackingNumber);
    //}

    //[Fact]
    //public async Task Track_UnknownTrackingNumber_Returns404()
    //{
    //    var response = await _client.GetAsync("/track/TRK-DOESNOTEXIST", TestContext.Current.CancellationToken);

    //    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    //}

    //[Fact]
    //public async Task Track_ResponseNeverLeaksTenantId()
    //{
    //    // Arrange
    //    var trackingNumber = $"TRK-{Guid.NewGuid():N}";
    //    await CreateShipmentAsync(trackingNumber);

    //    // Act
    //    var response = await _client.GetAsync($"/track/{trackingNumber}", TestContext.Current.CancellationToken);
    //    var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    //    // Assert — TenantId must never appear in the public tracking response
    //    raw.Should().NotContainAny(
    //        "tenantId", "TenantId", "tenant_id",
    //        TestClaimsFactory.DefaultTenantId.ToString());
    //}

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task CreateShipmentAsync(string trackingNumber)
    {
        var request = new
        {
            TrackingNumber = trackingNumber,
            CarrierType = "Steadfast",
            DestinationCity = "Dhaka"
        };

        var response = await _client
            .WithClaims(TestClaimsFactory.DefaultClaims())
            .PostAsJsonAsync("/v1/shipments", request);

        response.EnsureSuccessStatusCode();
    }
}