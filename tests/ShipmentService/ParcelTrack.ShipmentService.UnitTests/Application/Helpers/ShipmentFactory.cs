using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Enums;

namespace ParcelTrack.ShipmentService.UnitTests.Application.Helpers;

/// <summary>
/// Centralized factory for building Shipment aggregates in test scenarios.
/// Drives the real state machine — never sets properties directly.
/// This means tests validate actual domain behaviour, not just assignments.
/// </summary>
public static class ShipmentFactory
{
    public static Shipment Create(
        string trackingNumber = "STD-TEST-001",
        CarrierType carrierType = CarrierType.Steadfast,
        string? buyerEmail = "buyer@example.com",
        Guid? userId = null,
        Guid? tenantId = null)
    {
        return Shipment.Create(
            trackingNumber,
            carrierType,
            buyerEmail,
            userId ?? Guid.NewGuid(),
            tenantId ?? Guid.NewGuid());
    }

    public static Shipment WithStatus(ShipmentStatus status,
        string trackingNumber = "STD-TEST-001",
        Guid? tenantId = null)
    {
        var shipment = Create(trackingNumber: trackingNumber, tenantId: tenantId);

        switch (status)
        {
            case ShipmentStatus.Created:
                break;

            case ShipmentStatus.InTransit:
                shipment.UpdateStatus(ShipmentStatus.InTransit, "In transit", null);
                break;

            case ShipmentStatus.OutForDelivery:
                shipment.UpdateStatus(ShipmentStatus.InTransit, "In transit", null);
                shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Out for delivery", "Dhaka");
                break;

            case ShipmentStatus.Delivered:
                shipment.UpdateStatus(ShipmentStatus.InTransit, "In transit", null);
                shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Out for delivery", "Dhaka");
                shipment.UpdateStatus(ShipmentStatus.Delivered, "Delivered", "Dhaka");
                break;

            case ShipmentStatus.Failed:
                shipment.UpdateStatus(ShipmentStatus.InTransit, "In transit", null);
                shipment.UpdateStatus(ShipmentStatus.OutForDelivery, "Out for delivery", "Dhaka");
                shipment.UpdateStatus(ShipmentStatus.Failed, "Delivery failed", "Dhaka");
                break;

            case ShipmentStatus.Cancelled:
                shipment.Cancel("Cancelled for test");
                break;
        }

        return shipment;
    }
}