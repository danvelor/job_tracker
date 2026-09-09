using JobTracker.Common.Application;
using JobTracker.Modules.Jobs.Domain.Events;
using JobTracker.Modules.Jobs.IntegrationEvents;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Notifications;

/// <summary>
/// The translation architecture 4.2 puts downstream of the outbox: the
/// interceptor persists the domain event, and this turns it into the published
/// contract once the transaction has committed.
///
/// It holds no idempotency key and needs none (4.5). It only translates and
/// publishes, and all three consumers absorb a duplicate — so a replay costs a
/// republish and changes nothing.
/// </summary>
internal sealed class PublishJobCompletedHandler(IEventBus bus)
    : INotificationHandler<JobCompletedDomainEvent>
{
    public Task Handle(JobCompletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        bus.PublishAsync(
            new JobCompletedIntegrationEvent(
                // The domain event's own identity travels, so a consumer keying
                // off it is keying off a value that survives a replay (D-34).
                domainEvent.Id,
                domainEvent.JobId,
                domainEvent.CustomerId,
                domainEvent.OrganizationId,
                domainEvent.StartedAt,
                domainEvent.CompletedAt),
            cancellationToken);
}
