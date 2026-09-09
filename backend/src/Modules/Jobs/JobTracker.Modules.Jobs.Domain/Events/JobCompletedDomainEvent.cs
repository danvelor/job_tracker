using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.Events;

/// <summary>
/// FR-9 and FR-10: triggers invoice generation and a customer notification.
///
/// It carries <paramref name="StartedAt"/> because Billing prices the labour
/// window and Jobs must not know the rate (D-33). Adding a field to an internal
/// event costs nothing — only this module compiles against it, which is the
/// distinction architecture 4.1 draws.
/// </summary>
public sealed record JobCompletedDomainEvent(
    Guid JobId,
    Guid CustomerId,
    Guid OrganizationId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt) : DomainEvent;
