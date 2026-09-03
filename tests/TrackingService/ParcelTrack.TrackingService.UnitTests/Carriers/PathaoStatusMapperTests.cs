using FluentAssertions;
using ParcelTrack.TrackingService.Domain.Enums;
using ParcelTrack.TrackingService.Infrastructure.Carriers.Pathao;

namespace ParcelTrack.TrackingService.UnitTests.Carriers;

public sealed class PathaoStatusMapperTests
{
    [Theory]
    [InlineData("Pickup_Requested", CarrierStatus.Created)]
    [InlineData("Assigned_for_Pickup", CarrierStatus.Created)]
    [InlineData("Picked", CarrierStatus.InTransit)]
    [InlineData("At_the_Sorting_HUB", CarrierStatus.InTransit)]
    [InlineData("In_Transit", CarrierStatus.InTransit)]
    [InlineData("Received_at_Last_Mile_Hub", CarrierStatus.InTransit)]
    [InlineData("Assigned_for_Delivery", CarrierStatus.OutForDelivery)]
    [InlineData("Delivered", CarrierStatus.Delivered)]
    [InlineData("Partial_Delivery", CarrierStatus.Delivered)]
    [InlineData("Delivery_Failed", CarrierStatus.Failed)]
    [InlineData("Pickup_Failed", CarrierStatus.Failed)]
    [InlineData("On_Hold", CarrierStatus.Failed)]
    [InlineData("Returned", CarrierStatus.Returned)]
    [InlineData("Pickup_Cancelled", CarrierStatus.Cancelled)]
    public void ToCarrierStatus_ShouldMapKnownPathaoSlugs(string slug, CarrierStatus expected)
    {
        PathaoStatusMapper.ToCarrierStatus(slug).Should().Be(expected);
    }

    [Theory]
    [InlineData("Assigned_for_Delivery")]
    [InlineData("assigned-for-delivery")]
    [InlineData("ASSIGNED FOR DELIVERY")]
    [InlineData("AssignedForDelivery")]
    public void ToCarrierStatus_ShouldIgnoreCasingAndSeparators(string variant)
    {
        // Couriers spell the same state differently across their API, webhooks, and dashboard.
        PathaoStatusMapper.ToCarrierStatus(variant).Should().Be(CarrierStatus.OutForDelivery);
    }

    [Theory]
    [InlineData("Teleported_To_Buyer")]
    [InlineData("some_new_state_pathao_invented")]
    public void ToCarrierStatus_ShouldReturnUnknownForUnmappedStatus(string slug)
    {
        // A courier adding a state must never crash the polling worker.
        PathaoStatusMapper.ToCarrierStatus(slug).Should().Be(CarrierStatus.Unknown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToCarrierStatus_ShouldReturnUnknownForMissingStatus(string? slug)
    {
        PathaoStatusMapper.ToCarrierStatus(slug).Should().Be(CarrierStatus.Unknown);
    }
}
