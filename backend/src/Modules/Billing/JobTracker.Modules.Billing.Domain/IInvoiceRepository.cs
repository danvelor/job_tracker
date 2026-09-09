namespace JobTracker.Modules.Billing.Domain;

public interface IInvoiceRepository
{
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);

    /// <summary>
    /// The read behind idempotency (4.5). The unique constraint is the
    /// guarantee; this is what lets the handler absorb a replay quietly, so a
    /// violation never escapes and leaves an outbox row retrying for ever.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid jobId, DateTimeOffset jobCompletedAt, CancellationToken cancellationToken = default);
}
