using JobTracker.Modules.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Modules.Billing.Infrastructure;

/// <summary>
/// A schema of its own (architecture 6.1). Billing's context maps Billing's
/// tables and no others, so a query that reached into `jobs` would have no
/// DbSet to reach through — the boundary is enforced by the compiler and
/// visible in the database.
///
/// No tenant context: an invoice is written by a background consumer with no
/// request and no claim behind it. The organization arrives on the contract and
/// is written to the row, and a query filter that read an absent claim would
/// return nothing at all.
/// </summary>
public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public const string Schema = "billing";

    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
    }
}
