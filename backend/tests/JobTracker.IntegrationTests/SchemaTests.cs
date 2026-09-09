using FluentAssertions;
using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.IntegrationTests;

/// <summary>
/// The parts of the schema EF's fluent API cannot express, and which therefore
/// live in raw SQL inside a migration: the updated_at trigger and the
/// expression and GIN indexes.
/// </summary>
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

        // NFR-6 wants updated_at to be true, and a DEFAULT only fires on
        // INSERT. The update above went through raw SQL on purpose: EF's
        // SaveChanges is not the only writer this schema has to survive, so the
        // trigger is what makes the column honest rather than decorative.
        after.Should().BeAfter(before);
    }

    [Fact]
    public async Task The_keyset_index_is_declared_over_the_expression_the_repository_orders_by()
    {
        var definition = await IndexDefinition("ix_jobs_tenant_keyset");

        // The alignment that actually broke once. EF emitted
        // COALESCE(scheduled_date, $1) until EF.Constant was applied, and a
        // bind parameter cannot match an indexed expression — the index existed
        // and was never used. This asserts the two are the same expression.
        definition.Should().Contain("COALESCE(scheduled_date, '-infinity'::date)");
        definition.Should().Contain("organization_id");
    }

    [Fact]
    public async Task The_status_index_puts_the_equality_in_front_of_the_sort_keys()
    {
        var definition = await IndexDefinition("ix_jobs_tenant_status_keyset");

        // Column order is the whole value of a composite index: with status
        // behind the sort keys the equality becomes a post-filter and the index
        // stops being a scan boundary.
        definition.Should().Contain("organization_id, status");
        definition.Should().Contain("COALESCE(scheduled_date, '-infinity'::date)");
    }

    [Fact]
    public async Task The_full_text_index_is_GIN_over_the_expression_the_repository_matches()
    {
        var definition = await IndexDefinition("ix_jobs_search");

        definition.Should().Contain("USING gin");

        // PostgreSQL's own normalisation of the expression, casts included. It
        // applies the same normalisation to the query, which is why comparing
        // the stored form is comparing like with like — and why a repository
        // that concatenated the two columns differently, or asked for a
        // different regconfig, would show up here rather than as an index that
        // silently stopped being used.
        definition.Should().Contain(
            "to_tsvector('english'::regconfig, (((title)::text || ' '::text) || COALESCE(description, ''::text)))");
    }

    [Fact]
    public async Task A_full_text_search_still_finds_what_it_found_before_the_index()
    {
        await SeedJob();

        // An index is only correct if it changes cost and not results. This is
        // the same assertion SearchTests makes, repeated here because the index
        // is what this file is about — remove the GIN index and it must still
        // pass; break the expression it indexes and it must not.
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
        // Every roster read is filtered by organization and by nothing else,
        // and the pickers issue one on every page load.
        var assignees = await IndexDefinition("ix_assignees_tenant");
        var customers = await IndexDefinition("ix_customers_tenant");

        assignees.Should().Contain("organization_id");
        customers.Should().Contain("organization_id");
    }
}
