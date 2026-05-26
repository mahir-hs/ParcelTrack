using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ParcelTrack.WebhookDispatchService.Worker.Infrastructure;

/// <summary>
/// Used exclusively by EF Core tooling (dotnet ef migrations add / database update).
/// Never runs in production.
/// </summary>
public sealed class WebhookDbContextFactory : IDesignTimeDbContextFactory<WebhookDbContext>
{
    public WebhookDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WebhookDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=parceltrack_webhook;Username=postgres;Password=admin",
                npgsql => npgsql.MigrationsAssembly(typeof(WebhookDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new WebhookDbContext(options);
    }
}
