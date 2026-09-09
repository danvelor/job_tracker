using JobTracker.Common.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;
using JobTracker.Modules.Jobs.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.IntegrationTests;

[Collection(PostgresCollection.Name)]
public abstract class IntegrationTestBase(PostgresFixture postgres) : IAsyncLifetime
{
    // The seed is the source of these, not a literal repeated here. A job
    // carries foreign keys to both rosters (D-26), so a test that invents an
    // assignee identifier fails on a constraint rather than on its subject.
    protected static readonly Guid Organization = RosterSeed.DevelopmentOrganization;
    protected static readonly Guid OtherOrganization = RosterSeed.SecondOrganization;
    protected static readonly Guid Assignee = RosterSeed.AssigneeOrtiz;
    protected static readonly Guid Customer = RosterSeed.CustomerAcme;
    protected static readonly Guid OtherAssignee = RosterSeed.AssigneeOther;
    protected static readonly Guid OtherCustomer = RosterSeed.CustomerOther;

    protected JobsDbContext Context { get; private set; } = null!;

    /// <summary>
    /// Builds a context for another organization over the same data. Tenant
    /// isolation cannot be tested with one context: the filter has to be shown
    /// <em>not</em> returning rows a second tenant should never see.
    /// </summary>
    protected JobsDbContext ContextFor(Guid organizationId) =>
        new(BuildOptions(), new FixedTenantContext(organizationId));

    private DbContextOptions<JobsDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<JobsDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .UseSnakeCaseNamingConvention()
            // The same registration JobsModule makes. A harness without it
            // would test a context the application never builds.
            .AddInterceptors(new InsertOutboxMessagesInterceptor())
            .Options;

    public async Task InitializeAsync()
    {
        Context = new JobsDbContext(BuildOptions(), new FixedTenantContext(Organization));

        // A clean schema per test rather than a clean container: the same
        // isolation for a hundredth of the cost. Dropping and re-migrating also
        // means every test exercises the migration, which is case 1 of
        // architecture 8.2 running continuously rather than once.
        await Context.Database.EnsureDeletedAsync();
        await Context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await Context.DisposeAsync();
}
