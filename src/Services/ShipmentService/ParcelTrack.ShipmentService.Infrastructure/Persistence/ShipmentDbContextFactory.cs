using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ParcelTrack.ShipmentService.Application.Interfaces;

namespace ParcelTrack.ShipmentService.Infrastructure.Persistence;

/// <summary>
/// Used exclusively by EF Core tooling (dotnet ef migrations add / database update).
/// At design time there is no HTTP request, so ITenantContext cannot be resolved from DI.
/// This factory provides a stub tenant context so the tooling can create the DbContext.
/// Never runs in production — only during development/CI when running EF commands.
/// </summary>
public sealed class ShipmentDbContextFactory : IDesignTimeDbContextFactory<ShipmentDbContext>
{
    public ShipmentDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ShipmentDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=parceltrack_shipment;Username=postgres;Password=admin",
            npgsql => npgsql.MigrationsAssembly(typeof(ShipmentDbContext).Assembly.FullName));

        return new ShipmentDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }

    /// <summary>
    /// Stub ITenantContext for design-time use only.
    /// TenantId = Guid.Empty — never used to filter data, only satisfies the constructor.
    /// </summary>
    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid UserId => Guid.Empty;
    }
}