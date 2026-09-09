using JobTracker.Modules.Jobs.Domain;

namespace JobTracker.Modules.Jobs.Infrastructure.Repositories;

internal sealed partial class JobRepository
{
    /// <summary>
    /// Stages, never commits. <see cref="UnitOfWork"/> owns the transaction, so
    /// two commands in one request are atomic together — and in plan 4 the
    /// outbox rows join the same commit.
    /// </summary>
    public async Task AddAsync(Job job, CancellationToken cancellationToken = default) =>
        await context.Jobs.AddAsync(job, cancellationToken);
}
