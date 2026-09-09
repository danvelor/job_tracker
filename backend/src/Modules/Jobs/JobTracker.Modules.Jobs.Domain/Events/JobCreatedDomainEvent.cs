using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.Events;

/// <summary>
/// FR-8: triggers a notification to the assigned crew. It never becomes an
/// integration event, because notifying stays inside Jobs (D-22) — which is
/// what makes it the counter-example architecture 4.1 needs.
/// </summary>
public sealed record JobCreatedDomainEvent(Guid JobId, Guid AssigneeId, Guid OrganizationId)
    : DomainEvent;
