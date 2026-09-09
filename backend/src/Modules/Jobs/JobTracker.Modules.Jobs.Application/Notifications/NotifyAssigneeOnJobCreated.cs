using JobTracker.Common.Application;
using JobTracker.Modules.Jobs.Application.Abstractions;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Domain.Events;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Notifications;

/// <summary>
/// FR-8, and the counter-example architecture 4.1 needs: JobCreatedDomainEvent
/// never becomes an integration event, because notifying the crew stays inside
/// Jobs and no other module needs to know a job was created.
/// </summary>
internal sealed class NotifyAssigneeOnJobCreatedHandler(
    INotificationRepository notifications,
    IPartyRepository parties,
    IJobRepository jobs,
    IBackgroundQueue queue,
    IUnitOfWork unitOfWork,
    TimeProvider time) : INotificationHandler<JobCreatedDomainEvent>
{
    public async Task Handle(JobCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var assignee = (await parties.ListAssigneesAsync(domainEvent.OrganizationId, cancellationToken))
            .FirstOrDefault(candidate => candidate.Id == domainEvent.AssigneeId);

        if (assignee is null)
        {
            // Nobody to notify. Returning quietly lets the outbox row be
            // stamped: retrying forever would not conjure a crew member, and
            // an unprocessed row would block nothing else but would never
            // stop being retried.
            return;
        }

        // Idempotency, absorbed rather than thrown (4.5). A unique-constraint
        // violation escaping here would leave the outbox row unprocessed and
        // the drain would retry it every poll for good.
        if (await notifications.ExistsAsync(domainEvent.Id, assignee.Name, cancellationToken))
        {
            return;
        }

        var job = await jobs.GetByIdAsync(domainEvent.JobId, cancellationToken);

        var notification = Notification.Draft(
            domainEvent.Id,
            domainEvent.OrganizationId,
            assignee.Name,
            "A job has been assigned to you",
            $"{job?.Title ?? "A job"} is scheduled for {job?.ScheduledDate?.ToString("yyyy-MM-dd") ?? "an unset date"}.",
            time.GetUtcNow());

        if (notification.IsFailure)
        {
            return;
        }

        await notifications.AddAsync(notification.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Enqueued after the commit, not before: a job that started before the
        // row existed would look the notification up and find nothing.
        queue.Enqueue(
            new SendNotificationCommand(notification.Value.Id),
            domainEvent.OrganizationId);
    }
}
