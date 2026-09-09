using JobTracker.Modules.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Modules.Billing.Infrastructure.Repositories;

internal sealed class InvoiceRepository(BillingDbContext context) : IInvoiceRepository
{
    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default) =>
        await context.Invoices.AddAsync(invoice, cancellationToken);

    public Task<bool> ExistsAsync(
        Guid jobId, DateTimeOffset jobCompletedAt, CancellationToken cancellationToken = default) =>
        context.Invoices.AsNoTracking().AnyAsync(
            invoice => invoice.JobId == jobId && invoice.JobCompletedAt == jobCompletedAt,
            cancellationToken);
}
