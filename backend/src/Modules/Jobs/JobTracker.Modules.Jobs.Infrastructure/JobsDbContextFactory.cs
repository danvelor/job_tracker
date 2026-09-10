using JobTracker.Common.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobTracker.Modules.Jobs.Infrastructure;

internal sealed class JobsDbContextFactory : IDesignTimeDbContextFactory<JobsDbContext>
{
    public JobsDbContext CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<JobsDbContext>()
                .UseNpgsql("Host=localhost;Database=design_time_only")
                .UseSnakeCaseNamingConvention()
                .Options,
            new MutableTenantContext(Guid.Empty));
}
