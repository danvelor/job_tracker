namespace JobTracker.Modules.Jobs.Application.Abstractions;

/// <summary>
/// Architecture 4.4, role two: each outbound send is its own job, so a slow or
/// failing transport cannot stall the outbox drain and each send gets its own
/// retry schedule.
///
/// A port rather than <c>IBackgroundJobClient</c> directly, for the same reason
/// Application does not name EF: the layer states what it needs, and the host
/// decides that Hangfire provides it. It also lets a test run the work inline
/// and deterministically instead of standing up a job server.
///
/// <paramref name="organizationId"/> is a parameter rather than something the
/// runner infers, because a job runs minutes later in a scope of its own with
/// no request and therefore no claim behind it. Making the caller say whose
/// work it is means no enqueue can forget — which one did, and the failure was
/// invisible until the Compose stack ran it (see D-36).
/// </summary>
public interface IBackgroundQueue
{
    /// <summary>
    /// Records work to be done. Nothing goes out yet: a handler runs inside the
    /// outbox transaction, and the rows a job will read are not visible to any
    /// other connection until that transaction commits.
    /// </summary>
    void Enqueue<TRequest>(TRequest request, Guid organizationId) where TRequest : notnull;

    /// <summary>
    /// Dispatches what was recorded. Called by whoever owns the transaction,
    /// after committing it — never by a handler, which does not know when its
    /// own writes become visible.
    ///
    /// The split exists because the alternative was observed rather than
    /// imagined: dispatching on enqueue raced the commit, the worker looked up
    /// a notification that was not there, and the failure was silent (D-36).
    /// </summary>
    void Flush();
}
