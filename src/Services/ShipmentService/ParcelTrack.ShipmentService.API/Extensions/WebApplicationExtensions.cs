using Microsoft.EntityFrameworkCore;
using ParcelTrack.ShipmentService.API.Middleware;
using ParcelTrack.ShipmentService.Infrastructure.Persistence;

namespace ParcelTrack.ShipmentService.API.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures the middleware pipeline and runs dev-only setup (docs + migrations).
    /// Returns a Task because MigrateAsync is async — called directly from Program.cs
    /// to keep the top-level statements clean.
    /// </summary>
    public static async Task UseApiPipelineAsync(this WebApplication app)
    {
        // Apply any pending EF migrations before serving traffic.
        // Retries briefly so a slow-starting Postgres doesn't kill the API.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ShipmentDbContext>>();

            var attempts = 0;
            while (attempts < 5)
            {
                try
                {
                    await db.Database.MigrateAsync();
                    break;
                }
                catch (Exception ex)
                {
                    attempts++;
                    logger.LogWarning(ex, "Migration attempt {Attempt} failed; retrying...", attempts);
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }
        }

        // Must be first — wraps everything below
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.MapApiDocumentation();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        // Liveness probe used by the Gateway's active health check.
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
            .WithName("Health")
            .WithTags("health");
    }
}
