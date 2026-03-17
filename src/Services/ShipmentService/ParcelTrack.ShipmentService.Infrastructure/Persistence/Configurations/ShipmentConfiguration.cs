using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParcelTrack.ShipmentService.Domain.Entities;

namespace ParcelTrack.ShipmentService.Infrastructure.Persistence.Configurations;

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("shipments");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.TrackingNumber)
            .HasColumnName("tracking_number")
            .HasMaxLength(100)
            .IsRequired();

        // Tracking numbers are globally unique — a duplicate means the same parcel
        builder.HasIndex(s => s.TrackingNumber)
            .IsUnique()
            .HasDatabaseName("ix_shipments_tracking_number");

        builder.Property(s => s.CarrierType)
            .HasColumnName("carrier_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        // Index on tenant_id — every query is scoped to a tenant via global filter
        builder.HasIndex(s => s.TenantId)
            .HasDatabaseName("ix_shipments_tenant_id");

        builder.Property(s => s.BuyerEmail)
            .HasColumnName("buyer_email")
            .HasMaxLength(256);

        // Fixed: matches the actual property name on the domain entity
        builder.Property(s => s.DeliveryAttempts)
            .HasColumnName("delivery_attempts")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at");

        // DestinationCity — sufficient for public tracking page display ("Delivering to Chattogram").
        // Full address deferred until ParcelTrack handles shipment booking with carriers directly.
        builder.Property(s => s.DestinationCity)
            .HasColumnName("destination_city")
            .HasMaxLength(100);

        // ShipmentEvents — cascade delete: deleting a shipment removes all its events
        builder.HasMany(s => s.Events)
            .WithOne()
            .HasForeignKey(e => e.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}