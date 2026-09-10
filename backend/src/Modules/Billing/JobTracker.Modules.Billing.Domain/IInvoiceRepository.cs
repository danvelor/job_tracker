namespace JobTracker.Modules.Billing.Domain;

public interface IInvoiceRepository
{
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid jobId, DateTimeOffset jobCompletedAt, CancellationToken cancellationToken = default);
}
