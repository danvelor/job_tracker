using FluentAssertions;
using JobTracker.Api;
using JobTracker.Common.Infrastructure;
using JobTracker.Modules.Billing.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.IntegrationTests.Api;

/// <summary>
/// NFR-7: a reviewer runs `docker compose up` and nothing else. Migrations at
/// startup are the difference between one command and a README step people
/// skip — and they are the piece most likely to be wrong, because two modules
/// keep two histories in two schemas.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class MigrationStartupTests(PostgresFixture postgres) : IAsyncLifetime
{
    private ApiFactory _api = null!;

    public Task InitializeAsync()
    {
        _api = new ApiFactory(postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private async Task DropEverything()
    {
        using var scope = _api.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
        await context.Database.ExecuteSqlRawAsync(
            "drop schema if exists jobs cascade; drop schema if exists billing cascade");
    }

    private async Task Migrate()
    {
        using var scope = _api.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(default);
    }

    private async Task<int> TablesIn(string schema)
    {
        using var scope = _api.Services.CreateScope();
        using var _ = scope.ServiceProvider.GetRequiredService<ITenantContextSetter>()
            .Use(RosterSeed.DevelopmentOrganization);
        var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();

        return await context.Database.SqlQuery<int>(
            $"""
             select count(*)::int as "Value" from information_schema.tables
             where table_schema = {schema}
             """).SingleAsync();
    }

    [Fact]
    public async Task Starting_the_application_migrates_both_modules()
    {
        await DropEverything();

        await Migrate();

        // A migrator that ran one module would leave a system that starts,
        // serves a job list, and fails on the first completed job — three
        // layers from the cause.
        (await TablesIn("jobs")).Should().BeGreaterThan(0);
        (await TablesIn("billing")).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Migrating_a_database_that_is_already_current_is_a_no_op()
    {
        await DropEverything();
        await Migrate();

        var again = async () => await Migrate();

        // Compose restarts a failed container. A migrator that threw on the
        // second run would turn one transient fault into a crash loop.
        await again.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Migrating_seeds_the_rosters_the_pickers_offer()
    {
        await DropEverything();

        await Migrate();

        // Walkthrough step 2 picks a crew and a customer. A stack that migrated
        // but did not seed fails there, and the message would be about a
        // foreign key rather than about an empty dropdown.
        using var scope = _api.Services.CreateScope();
        using var _ = scope.ServiceProvider.GetRequiredService<ITenantContextSetter>()
            .Use(RosterSeed.DevelopmentOrganization);
        var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();

        (await context.Assignees.CountAsync()).Should().Be(2);
        (await context.Customers.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Each_module_keeps_its_history_in_its_own_schema()
    {
        await DropEverything();
        await Migrate();

        using var scope = _api.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobsDbContext>();

        var histories = await context.Database.SqlQuery<string>(
            $"""
             select table_schema as "Value" from information_schema.tables
             where table_name = '__EFMigrationsHistory' order by table_schema
             """).ToListAsync();

        // One history per module, each in the schema it owns. A shared history
        // is how each migrator reads the other's applied migrations as its own
        // and decides there is nothing to do.
        histories.Should().Equal("billing", "jobs");
    }
}
