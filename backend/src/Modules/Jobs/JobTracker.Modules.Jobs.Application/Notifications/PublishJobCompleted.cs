using JobTracker.Common.Application;
using JobTracker.Modules.Jobs.Domain.Events;
using JobTracker.Modules.Jobs.IntegrationEvents;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Notifications;

internal sealed class PublishJobCompletedHandler(IEventBus bus)
    : INotificationHandler<JobCompletedDomainEvent>
{
    public Task Handle(JobCompletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        bus.PublishAsync(
            new JobCompletedIntegrationEvent(
                domainEvent.Id,
                domainEvent.JobId,
                domainEvent.CustomerId,
                domainEvent.OrganizationId,
                domainEvent.StartedAt,
                domainEvent.CompletedAt),
            cancellationToken);
}
