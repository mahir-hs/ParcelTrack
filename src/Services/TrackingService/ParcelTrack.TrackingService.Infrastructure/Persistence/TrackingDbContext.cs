using Microsoft.EntityFrameworkCore;
using ParcelTrack.TrackingService.Domain.Entities;

namespace ParcelTrack.TrackingService.Infrastructure.Persistence;

public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : DbContext(options)
{
    public DbSet<TrackingRecord> TrackingRecords => Set<TrackingRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackingDbContext).Assembly);
    }
}
