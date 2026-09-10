using JobTracker.Modules.Jobs.Domain;

namespace JobTracker.Modules.Jobs.Infrastructure.Repositories;

internal sealed partial class JobRepository(JobsDbContext context) : IJobRepository;
