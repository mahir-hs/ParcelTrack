using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParcelTrack.ShipmentService.Domain.Entities;

namespace ParcelTrack.ShipmentService.Infrastructure.Persistence.Configurations;

public sealed class ShipmentEventConfiguration : IEntityTypeConfiguration<ShipmentEvent>
{
    public void Configure(EntityTypeBuilder<ShipmentEvent> builder)
    {
        builder.ToTable("shipment_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd(); ;

        builder.Property(e => e.ShipmentId)
            .HasColumnName("shipment_id")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(e => e.Location)
            .HasColumnName("location")
            .HasMaxLength(200);

        builder.Property(e => e.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        // Index on shipment_id — GetShipmentById always eager-loads events
        builder.HasIndex(e => e.ShipmentId)
            .HasDatabaseName("ix_shipment_events_shipment_id");
    }
}