using JobTracker.Common.Infrastructure;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Modules.Jobs.Infrastructure;

public sealed class JobsDbContext(
    DbContextOptions<JobsDbContext> options,
    ITenantContext tenant) : DbContext(options)
{
    public const string Schema = "jobs";

    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Assignee> Assignees => Set<Assignee>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Notification> Notifications => Set<Notification>();
    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    internal Guid OrganizationId => tenant.OrganizationId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobsDbContext).Assembly);

        modelBuilder.Entity<Job>().HasQueryFilter(job => job.OrganizationId == OrganizationId);
        modelBuilder.Entity<Assignee>()
            .HasQueryFilter(assignee => assignee.OrganizationId == OrganizationId);
        modelBuilder.Entity<Customer>()
            .HasQueryFilter(customer => customer.OrganizationId == OrganizationId);
        modelBuilder.Entity<Notification>()
            .HasQueryFilter(notification => notification.OrganizationId == OrganizationId);
    }
}
