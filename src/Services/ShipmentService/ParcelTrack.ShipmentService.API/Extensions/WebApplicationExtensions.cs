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

            // Auto-apply pending migrations on startup in dev — avoids manual dotnet ef database update
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
            await db.Database.MigrateAsync();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health");
    }
}
