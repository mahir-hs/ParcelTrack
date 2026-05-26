using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.Infrastructure.Persistence.Configurations;

public sealed class TrackingRecordConfiguration : IEntityTypeConfiguration<TrackingRecord>
{
    public void Configure(EntityTypeBuilder<TrackingRecord> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.TrackingNumber).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Status).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Description).IsRequired().HasMaxLength(500);
        builder.Property(r => r.CarrierType).HasMaxLength(50);
        builder.Property(r => r.Location).HasMaxLength(200);

        builder.HasIndex(r => r.TrackingNumber);
        builder.HasIndex(r => r.ShipmentId);
    }
}
