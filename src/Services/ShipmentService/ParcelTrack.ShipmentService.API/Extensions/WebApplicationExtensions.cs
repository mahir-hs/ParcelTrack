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
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
    }
}
