using JobTracker.Common.Application;
using JobTracker.Modules.Jobs.Application.Abstractions;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Domain.Events;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Notifications;

internal sealed class NotifyAssigneeOnJobCancelledHandler(
    INotificationRepository notifications,
    IPartyRepository parties,
    IJobRepository jobs,
    IBackgroundQueue queue,
    IUnitOfWork unitOfWork,
    TimeProvider time) : INotificationHandler<JobCancelledDomainEvent>
{
    public async Task Handle(JobCancelledDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.AssigneeId is not { } assigneeId)
        {
            return;
        }

        var assignee = (await parties.ListAssigneesAsync(domainEvent.OrganizationId, cancellationToken))
            .FirstOrDefault(candidate => candidate.Id == assigneeId);

        if (assignee is null)
        {
            return;
        }

        if (await notifications.ExistsAsync(domainEvent.Id, assignee.Name, cancellationToken))
        {
            return;
        }

        var job = await jobs.GetByIdAsync(domainEvent.JobId, cancellationToken);

        var notification = Notification.Draft(
            domainEvent.Id,
            domainEvent.OrganizationId,
            assignee.Name,
            "A job assigned to you has been cancelled",
            $"{job?.Title ?? "A job"} has been cancelled: {domainEvent.Reason}.",
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
