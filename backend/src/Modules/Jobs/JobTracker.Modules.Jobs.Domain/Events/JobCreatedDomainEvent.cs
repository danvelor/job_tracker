using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.Events;

public sealed record JobCreatedDomainEvent(Guid JobId, Guid AssigneeId) : DomainEvent;
