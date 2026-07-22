using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.Infrastructure.Persistence.Configurations;

public sealed class TrackingRecordConfiguration : IEntityTypeConfiguration<TrackingRecord>
{
    public void Configure(EntityTypeBuilder<TrackingRecord> builder)
    {
        builder.ToTable("tracking_records");

        builder.HasKey(r => r.ShipmentId);
        builder.Property(r => r.ShipmentId).HasColumnName("shipment_id");

        builder.Property(r => r.TrackingNumber).HasColumnName("tracking_number").HasMaxLength(100).IsRequired();
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.CarrierType).HasColumnName("carrier_type").HasMaxLength(50).IsRequired();
        builder.Property(r => r.CurrentStatus).HasColumnName("current_status").HasMaxLength(50).IsRequired();
        builder.Property(r => r.LastLocation).HasColumnName("last_location").HasMaxLength(200);
        builder.Property(r => r.LastEventAt).HasColumnName("last_event_at");

        builder.HasMany(r => r.Events)
            .WithOne()
            .HasForeignKey(e => e.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.TrackingNumber).HasDatabaseName("ix_tracking_records_tracking_number");
    }
}
