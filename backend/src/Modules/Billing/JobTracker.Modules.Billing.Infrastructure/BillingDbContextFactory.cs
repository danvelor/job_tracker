using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobTracker.Modules.Billing.Infrastructure;

/// <summary>For `dotnet ef` only, exactly as the Jobs one is.</summary>
internal sealed class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time_only")
            .UseSnakeCaseNamingConvention()
            .Options);
}
