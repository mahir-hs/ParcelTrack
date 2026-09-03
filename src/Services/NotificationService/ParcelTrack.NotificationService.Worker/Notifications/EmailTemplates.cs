namespace ParcelTrack.NotificationService.Worker.Notifications;

internal static class EmailTemplates
{
    internal static (string Subject, string Body) ShipmentCreated(string trackingNumber) => (
        Subject: $"Your shipment {trackingNumber} is on its way",
        Body: $"""
            Hi,

            Your shipment has been registered with ParcelTrack.

            Tracking Number: {trackingNumber}
            Status: Created

            You will receive further updates as your shipment progresses.

            — ParcelTrack
            """);

    internal static (string Subject, string Body) StatusChanged(string trackingNumber, string previousStatus, string newStatus) => (
        Subject: $"Shipment {trackingNumber} update: {newStatus}",
        Body: $"""
            Hi,

            Your shipment status has been updated.

            Tracking Number: {trackingNumber}
            Previous Status: {previousStatus}
            Current Status:  {newStatus}

            — ParcelTrack
            """);
}
