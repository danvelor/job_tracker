using JobTracker.Common.Application;
using JobTracker.Modules.Billing.Domain;
using JobTracker.Modules.Jobs.IntegrationEvents;
using Microsoft.Extensions.Options;

namespace JobTracker.Modules.Billing.Application;

/// <summary>
/// FR-9. It consumes a record of primitives and knows nothing else about Jobs —
/// no aggregate, no repository, no schema. That is the claim architecture 3.5
/// makes, and a layer rule reading the project graph is what keeps it true.
/// </summary>
internal sealed class GenerateInvoiceOnJobCompletedHandler(
    IInvoiceRepository invoices,
    IUnitOfWork unitOfWork,
    IOptions<BillingOptions> options,
    TimeProvider time) : IIntegrationEventHandler<JobCompletedIntegrationEvent>
{
    public async Task HandleAsync(
        JobCompletedIntegrationEvent completed, CancellationToken cancellationToken = default)
    {
        // Idempotency, absorbed rather than thrown (4.5). The key derives from
        // a completion timestamp, which does not change across a replay — the
        // condition that makes the rule sufficient.
        if (await invoices.ExistsAsync(completed.JobId, completed.CompletedAt, cancellationToken))
        {
            return;
        }

        var invoice = Invoice.Raise(
            completed.JobId,
            completed.CustomerId,
            completed.OrganizationId,
            completed.StartedAt,
            completed.CompletedAt,
            options.Value.Rate,
            time.GetUtcNow());

        if (invoice.IsFailure)
        {
            // A job that cannot be priced is a data problem, not a transient
            // one. Returning lets the outbox row be stamped: retrying would
            // never succeed, and the notification handlers on the same event
            // must not be blocked by it.
            return;
        }

        await invoices.AddAsync(invoice.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    public decimal RatePerHour { get; init; } = 90m;
    public decimal MinimumCharge { get; init; } = 100m;

    public LabourRate Rate => new(RatePerHour, MinimumCharge);
}
