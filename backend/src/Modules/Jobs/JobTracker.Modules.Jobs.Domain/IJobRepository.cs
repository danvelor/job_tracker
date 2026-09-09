namespace JobTracker.Modules.Jobs.Domain;

/// <summary>
/// Exactly the three members assessment line 220 names. There is deliberately
/// no generic IRepository&lt;T&gt; with Update, Delete, Count and GetAll: a Job
/// is never deleted — BR-2 says a closed job is corrected by a new job — and a
/// Delete a caller can see is a Delete someone eventually calls.
/// </summary>
public interface IJobRepository
{
    Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a projection rather than aggregates (D-25). Lines 220 and 208
    /// contradict each other — one puts SearchAsync in the domain, the other
    /// demands projections without tracking — and a projected read model is
    /// what satisfies both without leaving a dead method behind.
    /// </summary>
    Task<IReadOnlyList<JobSearchResult>> SearchAsync(
        JobSearchCriteria criteria, CancellationToken cancellationToken = default);
}
