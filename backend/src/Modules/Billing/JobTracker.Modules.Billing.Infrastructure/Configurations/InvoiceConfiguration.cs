using JobTracker.Modules.Billing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTracker.Modules.Billing.Infrastructure.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices", table =>
            table.HasCheckConstraint("ck_invoices_amount_positive", "amount > 0"));

        builder.HasKey(invoice => invoice.Id);
        builder.Property(invoice => invoice.Id).ValueGeneratedNever();
        builder.Ignore(invoice => invoice.DomainEvents);

        builder.Property(invoice => invoice.Amount).HasPrecision(12, 2).IsRequired();
        builder.Property(invoice => invoice.JobId).IsRequired();
        builder.Property(invoice => invoice.CustomerId).IsRequired();
        builder.Property(invoice => invoice.OrganizationId).IsRequired();

        // The business idempotency key line 246 asks for by name. Both parts
        // are stable across a replay, which is the condition architecture 4.5
        // puts on one.
        builder.HasIndex(invoice => new { invoice.JobId, invoice.JobCompletedAt })
            .IsUnique()
            .HasDatabaseName("uq_invoices_idempotency");

        builder.HasIndex(invoice => new { invoice.OrganizationId, invoice.JobId })
            .HasDatabaseName("ix_invoices_tenant_job");

        // No foreign key to jobs.jobs, and that absence is the module boundary
        // itself. A constraint there would let the database enforce a
        // relationship the two modules deliberately express through a contract,
        // and would turn extracting Billing into its own database from a
        // migration into a redesign.
    }
}
