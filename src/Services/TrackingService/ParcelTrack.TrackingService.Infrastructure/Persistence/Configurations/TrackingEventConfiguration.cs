using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.Infrastructure.Persistence.Configurations;

public sealed class TrackingEventConfiguration : IEntityTypeConfiguration<TrackingEvent>
{
    public void Configure(EntityTypeBuilder<TrackingEvent> builder)
    {
        builder.ToTable("tracking_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(e => e.ShipmentId).HasColumnName("shipment_id");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Location).HasColumnName("location").HasMaxLength(200);
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at");

        builder.HasIndex(e => e.ShipmentId).HasDatabaseName("ix_tracking_events_shipment_id");
    }
}
