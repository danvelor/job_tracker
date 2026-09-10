using JobTracker.Common.Application;
using JobTracker.Modules.Jobs.Application.Abstractions;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Domain.Events;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Notifications;

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
