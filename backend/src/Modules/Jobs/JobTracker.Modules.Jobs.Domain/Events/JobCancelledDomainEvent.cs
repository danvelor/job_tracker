using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.Events;

public sealed record JobCancelledDomainEvent(Guid JobId, Guid? AssigneeId, string Reason) : DomainEvent;
