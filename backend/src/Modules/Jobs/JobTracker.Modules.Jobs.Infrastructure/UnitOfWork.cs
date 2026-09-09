using JobTracker.Common.Application;

namespace JobTracker.Modules.Jobs.Infrastructure;

/// <summary>
/// Plan 4 registers the outbox interceptor on this context, and that is what
/// makes an aggregate's state change and its outbox rows one atomic commit.
/// Today it is the seam that lets a handler save without naming a DbContext.
/// </summary>
internal sealed class UnitOfWork(JobsDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
