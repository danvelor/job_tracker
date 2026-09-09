using Testcontainers.PostgreSql;

namespace JobTracker.IntegrationTests;

/// <summary>
/// One container for the whole run. Starting a container per test would add
/// roughly two seconds each, and the isolation a fresh container buys is the
/// same isolation a fresh schema buys for a hundredth of the cost — see
/// <see cref="IntegrationTestBase"/>.
///
/// The image is pinned to the one architecture 10.1 names, so the suite tests
/// the PostgreSQL the Compose stack runs rather than whatever <c>latest</c> is
/// on the day CI happens to pull it.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("jobtracker")
        .WithUsername("jobtracker")
        .WithPassword("jobtracker")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
