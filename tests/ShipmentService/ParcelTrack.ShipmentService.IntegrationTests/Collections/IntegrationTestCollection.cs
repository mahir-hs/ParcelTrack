using ParcelTrack.ShipmentService.IntegrationTests.Fixtures;

namespace ParcelTrack.ShipmentService.IntegrationTests.Collections;

/// <summary>
/// Declares a shared xUnit collection so PostgresContainerFixture is created once
/// and reused across all test classes — not restarted per class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection
    : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "IntegrationTests";
}