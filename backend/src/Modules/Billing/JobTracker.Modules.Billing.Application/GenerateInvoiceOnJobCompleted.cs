using JobTracker.Common.Application;
using JobTracker.Modules.Billing.Domain;
using JobTracker.Modules.Jobs.IntegrationEvents;
using Microsoft.Extensions.Options;

namespace JobTracker.Modules.Billing.Application;

internal sealed class GenerateInvoiceOnJobCompletedHandler(
    IInvoiceRepository invoices,
    IBillingUnitOfWork unitOfWork,
    IOptions<BillingOptions> options,
    TimeProvider time) : IIntegrationEventHandler<JobCompletedIntegrationEvent>
{
    public async Task HandleAsync(
        JobCompletedIntegrationEvent completed, CancellationToken cancellationToken = default)
    {
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
