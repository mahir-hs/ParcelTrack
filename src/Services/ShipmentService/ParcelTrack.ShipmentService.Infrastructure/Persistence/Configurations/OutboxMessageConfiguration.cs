using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParcelTrack.ShipmentService.Infrastructure.Persistence.Outbox;

namespace ParcelTrack.ShipmentService.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id");

        builder.Property(m => m.Topic)
            .HasColumnName("topic")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(m => m.Type)
            .HasColumnName("type")
            .HasMaxLength(300)
            .IsRequired();

        // Payload is serialized JSON — no length limit
        builder.Property(m => m.Payload)
            .HasColumnName("payload")
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(m => m.ProcessedAt)
            .HasColumnName("processed_at");

        builder.Property(m => m.Error)
            .HasColumnName("error");

        builder.Property(m => m.AttemptCount)
            .HasColumnName("attempt_count")
            .HasDefaultValue(0)
            .IsRequired();

        // OutboxProcessor's SELECT query: WHERE processed_at IS NULL ORDER BY created_at
        // This index makes that query fast regardless of table size
        builder.HasIndex(m => m.ProcessedAt)
            .HasDatabaseName("ix_outbox_messages_processed_at");
    }
}