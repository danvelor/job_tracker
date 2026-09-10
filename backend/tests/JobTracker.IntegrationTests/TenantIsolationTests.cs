using FluentAssertions;
using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.IntegrationTests;

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
        await using var seeding = ContextFor(Organization);
        seeding.Jobs.Add(AJob(Organization, Assignee, Customer, "Ours"));
        seeding.Jobs.Add(AJob(OtherOrganization, OtherAssignee, OtherCustomer, "Theirs"));
        await seeding.SaveChangesAsync();
    }

    [Fact]
    public async Task A_query_with_no_tenant_condition_returns_only_our_rows()
    {
        await SeedBothTenants();

        await using var ours = ContextFor(Organization);
        var titles = await ours.Jobs.Select(job => job.Title).ToListAsync();

        titles.Should().Equal("Ours");
    }

    [Fact]
    public async Task A_count_cannot_see_the_other_tenant()
    {
        await SeedBothTenants();

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

        found.Should().BeNull();
    }

    [Fact]
    public async Task The_rows_really_are_there_when_the_filter_is_lifted()
    {
        await SeedBothTenants();

        await using var ours = ContextFor(Organization);
        var all = await ours.Jobs.IgnoreQueryFilters().CountAsync();

        all.Should().Be(2);
    }

    [Fact]
    public async Task The_rosters_are_scoped_too()
    {
        await using var ours = ContextFor(Organization);
        var assignees = await ours.Assignees.Select(assignee => assignee.Name).ToListAsync();
        var customers = await ours.Customers.Select(customer => customer.Name).ToListAsync();

        assignees.Should().BeEquivalentTo(["J. Ortiz", "M. Ruiz"]);
        customers.Should().BeEquivalentTo(["Acme Holdings", "Birch Property"]);
    }
}
