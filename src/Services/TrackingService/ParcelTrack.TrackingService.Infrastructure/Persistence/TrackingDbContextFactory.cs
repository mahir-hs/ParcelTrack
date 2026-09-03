using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ParcelTrack.TrackingService.Infrastructure.Persistence;

/// <summary>
/// Used exclusively by EF Core tooling (dotnet ef migrations add / database update).
/// Never runs in production.
/// </summary>
public sealed class TrackingDbContextFactory : IDesignTimeDbContextFactory<TrackingDbContext>
{
    public TrackingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TrackingDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=parceltrack_tracking;Username=postgres;Password=admin",
                npgsql => npgsql.MigrationsAssembly(typeof(TrackingDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TrackingDbContext(options);
    }
}
