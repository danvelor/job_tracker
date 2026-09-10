using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.Events;

public sealed record JobCompletedDomainEvent(
    Guid JobId,
    Guid CustomerId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt) : DomainEvent;
