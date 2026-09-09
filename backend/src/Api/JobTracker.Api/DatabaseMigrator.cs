using JobTracker.Modules.Billing.Infrastructure;
using JobTracker.Modules.Jobs.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Api;

/// <summary>
/// NFR-7: `docker compose up` and nothing else — no manual migration step for a
/// reviewer to skip and then wonder why the job list is a 500.
///
/// Each module migrates its own context into its own schema with its own
/// history table, so adding a module is adding a line here. A shared history
/// would have each migrator read the other's applied migrations as its own and
/// conclude there was nothing to do.
/// </summary>
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
        // A scope of its own: the migrator is resolved once at startup and the
        // contexts are scoped, so borrowing the root provider's would keep two
        // contexts alive for the life of the process.
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pending.Count == 0)
        {
            // Not an error, and saying so matters: Compose restarts a failed
            // container, and a migrator that threw on an already-current
            // database would turn one transient fault into a crash loop.
            logger.LogInformation("{Context} is already current", typeof(TContext).Name);
            return;
        }

        logger.LogInformation(
            "Applying {Count} migration(s) to {Context}", pending.Count, typeof(TContext).Name);

        await context.Database.MigrateAsync(cancellationToken);
    }
}
