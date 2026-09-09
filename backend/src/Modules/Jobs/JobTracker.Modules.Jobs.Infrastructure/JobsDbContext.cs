using JobTracker.Common.Infrastructure;
using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Modules.Jobs.Infrastructure;

/// <summary>
/// One DbContext per module, mapping one schema. A query that reaches across
/// modules has no DbSet to reach through, so the boundary architecture 6.1
/// draws is enforced by the compiler rather than by discipline.
/// </summary>
public sealed class JobsDbContext(
    DbContextOptions<JobsDbContext> options,
    ITenantContext tenant) : DbContext(options)
{
    public const string Schema = "jobs";

    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Assignee> Assignees => Set<Assignee>();
    public DbSet<Customer> Customers => Set<Customer>();

    internal Guid OrganizationId => tenant.OrganizationId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobsDbContext).Assembly);

        // NFR-1, layer 2. The filter closes over `this`, so it reads the tenant
        // of the context the query runs on. It is declared here rather than in
        // the entity configurations because IEntityTypeConfiguration is static
        // and has no context to close over — a configuration-side filter would
        // capture nothing and silently compare against Guid.Empty.
        modelBuilder.Entity<Job>().HasQueryFilter(job => job.OrganizationId == OrganizationId);
        modelBuilder.Entity<Assignee>()
            .HasQueryFilter(assignee => assignee.OrganizationId == OrganizationId);
        modelBuilder.Entity<Customer>()
            .HasQueryFilter(customer => customer.OrganizationId == OrganizationId);
    }
}
