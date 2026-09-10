using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.IntegrationTests;

public sealed class HarnessTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task The_container_serves_the_PostgreSQL_the_Compose_stack_pins()
    {
        var version = await Context.Database
            .SqlQuery<string>($"""select version() as "Value" """)
            .SingleAsync();

        version.Should().StartWith("PostgreSQL 17");
    }

    [Fact]
    public async Task Migrations_apply_to_an_empty_database()
    {
        var applied = await Context.Database.GetAppliedMigrationsAsync();

        applied.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Each_test_starts_from_a_clean_schema()
    {
        var jobs = await Context.Jobs.IgnoreQueryFilters().CountAsync();

        jobs.Should().Be(0);
    }
}
