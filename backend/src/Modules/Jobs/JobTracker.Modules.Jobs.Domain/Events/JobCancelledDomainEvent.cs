using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.Events;

/// <summary>
/// Nothing outside Jobs consumes this: cancelling neither bills nor notifies.
/// It is the second internal-only event, and the pair with JobCompleted is what
/// makes the domain-versus-integration distinction demonstrable.
/// </summary>
public sealed record JobCancelledDomainEvent(Guid JobId, string Reason) : IDomainEvent;
