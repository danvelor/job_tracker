namespace JobTracker.Modules.Jobs.Domain;

public interface IJobRepository
{
    Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Job job, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobSearchResult>> SearchAsync(
        JobSearchCriteria criteria, CancellationToken cancellationToken = default);
}
