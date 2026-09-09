using JobTracker.Common.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobTracker.Modules.Jobs.Infrastructure;

/// <summary>
/// <c>dotnet ef</c> builds a context without DI, and this one needs a tenant.
/// The tenant is irrelevant to generating a schema, so the factory supplies an
/// empty one: this type exists for the tooling and is never resolved by the
/// application.
/// </summary>
internal sealed class JobsDbContextFactory : IDesignTimeDbContextFactory<JobsDbContext>
{
    public JobsDbContext CreateDbContext(string[] args) =>
        new(
            new DbContextOptionsBuilder<JobsDbContext>()
                .UseNpgsql("Host=localhost;Database=design_time_only")
                .UseSnakeCaseNamingConvention()
                .Options,
            new FixedTenantContext(Guid.Empty));
}
