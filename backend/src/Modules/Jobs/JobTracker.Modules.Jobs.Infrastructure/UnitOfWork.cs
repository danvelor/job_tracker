using JobTracker.Common.Application;

namespace JobTracker.Modules.Jobs.Infrastructure;

internal sealed class UnitOfWork(JobsDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
