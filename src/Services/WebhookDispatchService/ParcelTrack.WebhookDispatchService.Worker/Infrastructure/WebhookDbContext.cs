using Microsoft.EntityFrameworkCore;
using ParcelTrack.WebhookDispatchService.Worker.Domain;

namespace ParcelTrack.WebhookDispatchService.Worker.Infrastructure;

public sealed class WebhookDbContext(DbContextOptions<WebhookDbContext> options) : DbContext(options)
{
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WebhookSubscription>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.TargetUrl).IsRequired().HasMaxLength(2000);
            b.Property(x => x.Secret).HasMaxLength(256);
            b.HasIndex(x => x.TenantId);
        });

        modelBuilder.Entity<WebhookDelivery>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.EventType).IsRequired().HasMaxLength(100);
            b.Property(x => x.Payload).IsRequired();
            b.Property(x => x.ErrorMessage).HasMaxLength(1000);
            b.HasIndex(x => x.SubscriptionId);
        });
    }
}
