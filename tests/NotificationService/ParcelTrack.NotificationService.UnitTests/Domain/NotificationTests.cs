using FluentAssertions;
using ParcelTrack.NotificationService.Application.Domain;
using Xunit;

namespace ParcelTrack.NotificationService.UnitTests.Domain;

public sealed class NotificationTests
{
    private static readonly Guid ShipmentId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string TrackingNumber = "TRK-12345";
    private const string Status = "Delivered";

    [Fact]
    public void Create_WithBuyerEmail_SetsRecipientToBuyerEmail()
    {
        // Arrange
        const string buyerEmail = "buyer@example.com";

        // Act
        var notification = Notification.Create(ShipmentId, TenantId, UserId, Status, TrackingNumber, buyerEmail);

        // Assert
        notification.Recipient.Should().Be(buyerEmail);
    }

    [Fact]
    public void Create_WithNullBuyerEmail_FallsBackToOpsAddress()
    {
        // Act
        var notification = Notification.Create(ShipmentId, TenantId, UserId, Status, TrackingNumber, null);

        // Assert
        notification.Recipient.Should().Be("ops@parceltrack.dev");
    }

    [Fact]
    public void Create_SubjectReferencesTrackingNumberAndStatus()
    {
        // Act
        var notification = Notification.Create(ShipmentId, TenantId, UserId, Status, TrackingNumber, null);

        // Assert
        notification.Subject.Should().Contain(TrackingNumber);
        notification.Subject.Should().Contain(Status);
    }

    [Fact]
    public void Create_BodyReferencesTrackingNumberAndStatus()
    {
        // Act
        var notification = Notification.Create(ShipmentId, TenantId, UserId, Status, TrackingNumber, null);

        // Assert
        notification.Body.Should().Contain(TrackingNumber);
        notification.Body.Should().Contain(Status);
    }

    [Fact]
    public void Create_InitializesPendingState()
    {
        // Act
        var notification = Notification.Create(ShipmentId, TenantId, UserId, Status, TrackingNumber, null);

        // Assert
        notification.Status.Should().Be("Pending");
        notification.Attempts.Should().Be(0);
        notification.SentAt.Should().BeNull();
        notification.Error.Should().BeNull();
    }

    [Fact]
    public void MarkSent_SetsStatusSentAndClearsError()
    {
        // Arrange
        var notification = Notification.Create(ShipmentId, TenantId, UserId, Status, TrackingNumber, null);
        notification.RecordFailure("boom");

        // Act
        notification.MarkSent();

        // Assert
        notification.Status.Should().Be("Sent");
        notification.SentAt.Should().NotBeNull();
        notification.Error.Should().BeNull();
    }

    [Fact]
    public void RecordFailure_IncrementsAttemptsAndStoresError()
    {
        // Arrange
        var notification = Notification.Create(ShipmentId, TenantId, UserId, Status, TrackingNumber, null);

        // Act
        notification.RecordFailure("transient failure");

        // Assert
        notification.Attempts.Should().Be(1);
        notification.Error.Should().Be("transient failure");
        notification.Status.Should().Be("Pending");
        notification.ShouldDeadLetter.Should().BeFalse();
    }

    [Fact]
    public void RecordFailure_WhenAttemptsReachMax_SetsStatusFailed()
    {
        // Arrange
        var notification = Notification.Create(ShipmentId, TenantId, UserId, Status, TrackingNumber, null);

        // Act — MaxAttempts is 3
        notification.RecordFailure("attempt 1");
        notification.RecordFailure("attempt 2");
        notification.RecordFailure("attempt 3");

        // Assert
        notification.Attempts.Should().Be(3);
        notification.Status.Should().Be("Failed");
    }

    [Fact]
    public void ShouldDeadLetter_IsTrueOnlyWhenStatusIsFailed()
    {
        // Arrange
        var pending = Notification.Create(ShipmentId, TenantId, UserId, Status, TrackingNumber, null);
        var failed = Notification.Create(ShipmentId, TenantId, UserId, Status, TrackingNumber, null);
        failed.RecordFailure("1");
        failed.RecordFailure("2");
        failed.RecordFailure("3");

        // Assert
        pending.ShouldDeadLetter.Should().BeFalse();
        failed.ShouldDeadLetter.Should().BeTrue();
    }
}
