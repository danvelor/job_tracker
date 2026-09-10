using FluentAssertions;
using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.IntegrationTests;

public sealed class SchemaTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private async Task<Guid> SeedJob()
    {
        var job = Job.Create(
            "Ridge tile replacement", "north slope",
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2099, 5, 1), Assignee, Customer, Organization, Now).Value;
        Context.Jobs.Add(job);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return job.Id;
    }

    private Task<string> IndexDefinition(string name) =>
        Context.Database
            .SqlQuery<string>(
                $"""select indexdef as "Value" from pg_indexes where indexname = {name}""")
            .SingleAsync();

    [Fact]
    public async Task Updating_a_job_moves_updated_at_forward()
    {
        var id = await SeedJob();
        var before = await Context.Database
            .SqlQuery<DateTimeOffset>(
                $"""select updated_at as "Value" from jobs.jobs where id = {id}""")
            .SingleAsync();

        await Context.Database.ExecuteSqlAsync(
            $"update jobs.jobs set title = 'Renamed' where id = {id}");

        var after = await Context.Database
            .SqlQuery<DateTimeOffset>(
                $"""select updated_at as "Value" from jobs.jobs where id = {id}""")
            .SingleAsync();

        after.Should().BeAfter(before);
    }

    [Fact]
    public async Task The_keyset_index_is_declared_over_the_expression_the_repository_orders_by()
    {
        var definition = await IndexDefinition("ix_jobs_tenant_keyset");

        definition.Should().Contain("COALESCE(scheduled_date, '-infinity'::date)");
        definition.Should().Contain("organization_id");
    }

    [Fact]
    public async Task The_status_index_puts_the_equality_in_front_of_the_sort_keys()
    {
        var definition = await IndexDefinition("ix_jobs_tenant_status_keyset");

        definition.Should().Contain("organization_id, status");
        definition.Should().Contain("COALESCE(scheduled_date, '-infinity'::date)");
    }

    [Fact]
    public async Task The_full_text_index_is_GIN_over_the_expression_the_repository_matches()
    {
        var definition = await IndexDefinition("ix_jobs_search");

        definition.Should().Contain("USING gin");

        definition.Should().Contain(
            "to_tsvector('english'::regconfig, (((title)::text || ' '::text) || COALESCE(description, ''::text)))");
    }

    [Fact]
    public async Task A_full_text_search_still_finds_what_it_found_before_the_index()
    {
        await SeedJob();

        var rows = await Context.Jobs
            .Where(job => EF.Functions
                .ToTsVector("english", job.Title + " " + (job.Description ?? string.Empty))
                .Matches(EF.Functions.WebSearchToTsQuery("english", "slope")))
            .ToListAsync();

        rows.Should().ContainSingle();
    }

    [Fact]
    public async Task Both_rosters_are_indexed_by_tenant()
    {
        var assignees = await IndexDefinition("ix_assignees_tenant");
        var customers = await IndexDefinition("ix_customers_tenant");

        assignees.Should().Contain("organization_id");
        customers.Should().Contain("organization_id");
    }
}
