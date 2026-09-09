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
/// </summary>
public interface IBackgroundQueue
{
    void Enqueue<TRequest>(TRequest request) where TRequest : notnull;
}
