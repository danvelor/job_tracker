using JobTracker.Common.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure.Configurations;
using JobTracker.Modules.Jobs.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.IntegrationTests;

[Collection(PostgresCollection.Name)]
public abstract class IntegrationTestBase(PostgresFixture postgres) : IAsyncLifetime
{
    protected static readonly Guid Organization = RosterSeed.DevelopmentOrganization;
    protected static readonly Guid OtherOrganization = RosterSeed.SecondOrganization;
    protected static readonly Guid Assignee = RosterSeed.AssigneeOrtiz;
    protected static readonly Guid Customer = RosterSeed.CustomerAcme;
    protected static readonly Guid OtherAssignee = RosterSeed.AssigneeOther;
    protected static readonly Guid OtherCustomer = RosterSeed.CustomerOther;

    protected JobsDbContext Context { get; private set; } = null!;

    protected MutableTenantContext Tenant { get; } = new(RosterSeed.DevelopmentOrganization);

    protected JobsDbContext ContextFor(Guid organizationId) =>
        new(BuildOptions(), new MutableTenantContext(organizationId));

    private DbContextOptions<JobsDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<JobsDbContext>()
            .UseNpgsql(postgres.ConnectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", JobsDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new InsertOutboxMessagesInterceptor())
            .Options;

    public async Task InitializeAsync()
    {
        Context = new JobsDbContext(BuildOptions(), Tenant);

        await Context.Database.EnsureDeletedAsync();
        await Context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await Context.DisposeAsync();
}
