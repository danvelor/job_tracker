using FluentAssertions;
using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.IntegrationTests;

/// <summary>
/// Case 4 of architecture 8.2, and the requirement most likely to be believed
/// rather than checked: a mocked repository proves nothing about a filter that
/// lives below the query.
/// </summary>
public sealed class TenantIsolationTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static Job AJob(Guid organizationId, Guid assignee, Guid customer, string title) =>
        Job.Create(
            title, null,
            Address.Create("12 Elm St", "Springfield", "IL", "62701", 39.78m, -89.65m).Value,
            new DateOnly(2099, 3, 14), assignee, customer, organizationId, Now).Value;

    private async Task SeedBothTenants()
    {
        // One context with the filter lifted, because seeding has to write rows
        // this tenant is not allowed to read back.
        await using var seeding = ContextFor(Organization);
        seeding.Jobs.Add(AJob(Organization, Assignee, Customer, "Ours"));
        seeding.Jobs.Add(AJob(OtherOrganization, OtherAssignee, OtherCustomer, "Theirs"));
        await seeding.SaveChangesAsync();
    }

    [Fact]
    public async Task A_query_with_no_tenant_condition_returns_only_our_rows()
    {
        await SeedBothTenants();

        // Deliberately no Where on OrganizationId. That is the whole of NFR-1:
        // isolation must not depend on a caller remembering to filter.
        await using var ours = ContextFor(Organization);
        var titles = await ours.Jobs.Select(job => job.Title).ToListAsync();

        titles.Should().Equal("Ours");
    }

    [Fact]
    public async Task A_count_cannot_see_the_other_tenant()
    {
        await SeedBothTenants();

        // FR-11 names counts and aggregates separately, because a count that
        // leaks tells a competitor how much business the other one has without
        // ever showing them a row.
        await using var ours = ContextFor(Organization);
        var count = await ours.Jobs.CountAsync();

        count.Should().Be(1);
    }

    [Fact]
    public async Task A_direct_fetch_by_identifier_cannot_cross_the_boundary()
    {
        await SeedBothTenants();
        await using var theirs = ContextFor(OtherOrganization);
        var theirJobId = await theirs.Jobs.Select(job => job.Id).SingleAsync();

        await using var ours = ContextFor(Organization);
        var found = await ours.Jobs.SingleOrDefaultAsync(job => job.Id == theirJobId);

        // The dangerous case: an identifier guessed, logged or leaked from
        // anywhere else must not become a read.
        found.Should().BeNull();
    }

    [Fact]
    public async Task The_rows_really_are_there_when_the_filter_is_lifted()
    {
        await SeedBothTenants();

        await using var ours = ContextFor(Organization);
        var all = await ours.Jobs.IgnoreQueryFilters().CountAsync();

        // Without this, every assertion above would also pass against an empty
        // table and the suite would prove nothing.
        all.Should().Be(2);
    }

    [Fact]
    public async Task The_rosters_are_scoped_too()
    {
        // The seed put a third assignee and a third customer in the second
        // organization. A job list is useless without names, so the pickers
        // that supply them are as much a leak as the jobs themselves.
        await using var ours = ContextFor(Organization);
        var assignees = await ours.Assignees.Select(assignee => assignee.Name).ToListAsync();
        var customers = await ours.Customers.Select(customer => customer.Name).ToListAsync();

        assignees.Should().BeEquivalentTo(["J. Ortiz", "M. Ruiz"]);
        customers.Should().BeEquivalentTo(["Acme Holdings", "Birch Property"]);
    }
}
