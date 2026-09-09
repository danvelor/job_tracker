using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.Events;

/// <summary>FR-9 and FR-10: triggers invoice generation and a customer notification.</summary>
public sealed record JobCompletedDomainEvent(
    Guid JobId, Guid CustomerId, Guid OrganizationId, DateTimeOffset CompletedAt) : IDomainEvent;
