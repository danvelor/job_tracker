using JobTracker.Modules.Billing.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Api;

internal sealed class DatabaseMigrator(
    IServiceProvider services, ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await Migrate<JobsDbContext>(cancellationToken);
        await Migrate<BillingDbContext>(cancellationToken);
    }

    private async Task Migrate<TContext>(CancellationToken cancellationToken)
        where TContext : DbContext
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pending.Count == 0)
        {
            logger.LogInformation("{Context} is already current", typeof(TContext).Name);
            return;
        }

        logger.LogInformation(
            "Applying {Count} migration(s) to {Context}", pending.Count, typeof(TContext).Name);

        await context.Database.MigrateAsync(cancellationToken);
    }
}
