using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.Events;

/// <summary>
/// FR-12: triggers a notification to the crew that was going to do the work.
/// Nothing outside Jobs consumes it — cancelling notifies but never bills. It
/// is the second internal-only event, and the pair with JobCompleted is what
/// makes the domain-versus-integration distinction demonstrable: what sends an
/// event across the boundary is another module needing it, not the event
/// having consequences.
///
/// It carries <paramref name="AssigneeId"/> for the same reason JobCompleted
/// carries the customer, and nullable because a job cancelled before it was
/// scheduled never reached a crew.
/// </summary>
public sealed record JobCancelledDomainEvent(Guid JobId, Guid? AssigneeId, string Reason) : DomainEvent;
