using JobTracker.Modules.Jobs.Domain;

namespace JobTracker.Modules.Jobs.Infrastructure.Repositories;

internal sealed partial class JobRepository
{
    public async Task AddAsync(Job job, CancellationToken cancellationToken = default) =>
        await context.Jobs.AddAsync(job, cancellationToken);
}
