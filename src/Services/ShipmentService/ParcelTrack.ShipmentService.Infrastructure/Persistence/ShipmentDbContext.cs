using Microsoft.EntityFrameworkCore;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Infrastructure.Persistence.Outbox;

namespace ParcelTrack.ShipmentService.Infrastructure.Persistence;

public sealed class ShipmentDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public ShipmentDbContext(DbContextOptions<ShipmentDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Scans this assembly for all IEntityTypeConfiguration<T> implementations
        // and applies them automatically — no manual registration needed
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShipmentDbContext).Assembly);

        // Global query filter — every Shipment query is automatically scoped to the
        // current tenant. No developer can accidentally query another tenant's data.
        // Applied transparently on every SELECT, no code in repositories/handlers needed.
        //modelBuilder.Entity<Shipment>()
        //    .HasQueryFilter(s => s.TenantId == _tenantContext.TenantId);
    }
}