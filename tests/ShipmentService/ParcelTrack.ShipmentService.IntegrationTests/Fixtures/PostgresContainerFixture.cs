using Testcontainers.PostgreSql;

namespace ParcelTrack.ShipmentService.IntegrationTests.Fixtures;

/// <summary>
/// Spins up a real PostgreSQL container once for the entire test session.
/// Shared across all test classes via xUnit collection fixture.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =  new PostgreSqlBuilder("postgres:16")
    .WithDatabase("parceltrack_test")
    .WithUsername("postgres")
    .WithPassword("postgres")
    .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();
    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}