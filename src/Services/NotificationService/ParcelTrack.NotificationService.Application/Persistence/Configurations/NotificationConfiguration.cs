using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParcelTrack.NotificationService.Application.Domain;

namespace ParcelTrack.NotificationService.Application.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("id");

        builder.Property(n => n.ShipmentId).HasColumnName("shipment_id");
        builder.Property(n => n.TenantId).HasColumnName("tenant_id");
        builder.Property(n => n.UserId).HasColumnName("user_id");
        builder.Property(n => n.Channel).HasColumnName("channel").HasMaxLength(20).IsRequired();
        builder.Property(n => n.Recipient).HasColumnName("recipient").HasMaxLength(256).IsRequired();
        builder.Property(n => n.Subject).HasColumnName("subject").HasMaxLength(256).IsRequired();
        builder.Property(n => n.Body).HasColumnName("body").IsRequired();
        builder.Property(n => n.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(n => n.Attempts).HasColumnName("attempts");
        builder.Property(n => n.Error).HasColumnName("error").HasColumnType("text");
        builder.Property(n => n.CreatedAt).HasColumnName("created_at");
        builder.Property(n => n.SentAt).HasColumnName("sent_at");

        builder.HasIndex(n => n.ShipmentId).HasDatabaseName("ix_notifications_shipment_id");
        builder.HasIndex(n => n.Status).HasDatabaseName("ix_notifications_status");
    }
}
