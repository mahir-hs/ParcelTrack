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
        // Must be first — wraps everything below
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.MapApiDocumentation();
            await app.RunMigrationsAsync();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
    }

    /// <summary>
    /// Applies any pending EF Core migrations on startup.
    /// Dev convenience only — remove before deploying to AWS (CI/CD handles migrations there).
    /// </summary>
    private static async Task RunMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
        //await db.Database.MigrateAsync();
    }
}
