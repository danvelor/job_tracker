using JobTracker.Common.Application;
using JobTracker.Modules.Jobs.Application.Abstractions;
using JobTracker.Modules.Jobs.Domain;
using JobTracker.Modules.Jobs.Domain.Events;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Notifications;

/// <summary>
/// FR-12, and the sharper half of the counter-example architecture 4.1 needs.
/// Cancelling has a consequence — the crew must be told not to turn up — and
/// that consequence still never leaves Jobs. An internal event with real work
/// behind it draws the domain-versus-integration line better than one with no
/// consequence at all: what makes JobCompleted cross is Billing, not the fact
/// that something happens.
/// </summary>
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
            // A job cancelled before it reached a crew has nobody to tell.
            return;
        }

        var assignee = (await parties.ListAssigneesAsync(domainEvent.OrganizationId, cancellationToken))
            .FirstOrDefault(candidate => candidate.Id == assigneeId);

        if (assignee is null)
        {
            return;
        }

        // The key is (source event, recipient), so this row coexists with the
        // one FR-8 wrote for the same crew member about the same job.
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
            // BR-5 makes the reason mandatory so the cancellation can be
            // reviewed later. The crew is internal staff and the first
            // reviewer, so they get it rather than a bare notice.
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
