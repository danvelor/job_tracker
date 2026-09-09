using JobTracker.Common.Application;
using JobTracker.Modules.Jobs.Application.Abstractions;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Domain.Events;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Notifications;

/// <summary>
/// FR-10. It consumes the <em>domain</em> event rather than the contract,
/// because it lives inside Jobs and has no reason to go through a boundary to
/// reach its own module's data — the contract exists for Billing.
///
/// It writes a second row keyed (source_event_id, recipient). The recipient
/// differs from FR-8's, which is why one unique constraint serves both handlers.
/// </summary>
internal sealed class NotifyCustomerOnJobCompletedHandler(
    INotificationRepository notifications,
    IPartyRepository parties,
    IJobRepository jobs,
    IBackgroundQueue queue,
    IUnitOfWork unitOfWork,
    TimeProvider time) : INotificationHandler<JobCompletedDomainEvent>
{
    public async Task Handle(JobCompletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var customer = (await parties.ListCustomersAsync(domainEvent.OrganizationId, cancellationToken))
            .FirstOrDefault(candidate => candidate.Id == domainEvent.CustomerId);

        if (customer is null)
        {
            return;
        }

        if (await notifications.ExistsAsync(domainEvent.Id, customer.Email, cancellationToken))
        {
            return;
        }

        var job = await jobs.GetByIdAsync(domainEvent.JobId, cancellationToken);

        var notification = Notification.Draft(
            domainEvent.Id,
            domainEvent.OrganizationId,
            // The email, not the name: this one leaves the building.
            customer.Email,
            "Your job has been completed",
            $"{job?.Title ?? "Your job"} was completed on "
            + $"{domainEvent.CompletedAt:yyyy-MM-dd}. A signature was captured on site.",
            time.GetUtcNow());

        if (notification.IsFailure)
        {
            return;
        }

        await notifications.AddAsync(notification.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        queue.Enqueue(
            new SendNotificationCommand(notification.Value.Id),
            domainEvent.OrganizationId);
    }
}
