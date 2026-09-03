using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.Infrastructure.Persistence.Configurations;

public sealed class TrackedShipmentConfiguration : IEntityTypeConfiguration<TrackedShipment>
{
    public void Configure(EntityTypeBuilder<TrackedShipment> builder)
    {
        builder.ToTable("tracked_shipments");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TrackingNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.CarrierType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(s => s.LastKnownStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(s => s.BuyerEmail).HasMaxLength(320);
        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.IsActive).IsRequired();

        // One row per consignment — a courier's tracking number is the natural key, and this
        // is what stops a replayed ShipmentCreated event creating a duplicate to poll.
        builder.HasIndex(s => s.TrackingNumber).IsUnique();

        // The poll cycle's only query: active parcels for one carrier, oldest-polled first.
        builder.HasIndex(s => new { s.CarrierType, s.IsActive, s.LastPolledAt });
    }
}
