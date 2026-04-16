using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ParcelTrack.ShipmentService.Infrastructure.Persistence;
using ParcelTrack.ShipmentService.IntegrationTests.Auth;

namespace ParcelTrack.ShipmentService.IntegrationTests.Fixtures;

/// <summary>
/// Customises the test host:
///   - Swaps the real DbContext connection string for the Testcontainers one
///   - Replaces Keycloak JWT auth with TestAuthHandler
///   - Runs EF migrations so the schema is ready before tests execute
/// </summary>
public sealed class ShipmentServiceFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ShipmentServiceFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ShipmentDb"] = _connectionString
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });
        });
    }
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShipmentDbContext>();
        db.Database.EnsureCreated();

        return host;
    }
}

