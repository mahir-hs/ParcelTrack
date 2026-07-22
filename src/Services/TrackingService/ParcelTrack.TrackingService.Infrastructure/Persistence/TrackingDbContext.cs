using Microsoft.EntityFrameworkCore;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.Infrastructure.Persistence;

public sealed class TrackingDbContext : DbContext
{
    public TrackingDbContext(DbContextOptions<TrackingDbContext> options) : base(options) { }

    public DbSet<TrackingRecord> TrackingRecords => Set<TrackingRecord>();
    public DbSet<TrackingEvent> TrackingEvents => Set<TrackingEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackingDbContext).Assembly);
    }
}
