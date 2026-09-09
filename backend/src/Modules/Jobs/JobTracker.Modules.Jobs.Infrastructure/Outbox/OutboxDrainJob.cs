using Microsoft.Extensions.Logging;

namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

/// <summary>
/// What Hangfire actually schedules. It exists so the recurring job names a
/// type with a parameterless method rather than a lambda closing over a scoped
/// DbContext, which Hangfire would serialise and then fail to rebuild.
/// </summary>
public sealed class OutboxDrainJob(OutboxProcessor processor, ILogger<OutboxDrainJob> logger)
{
    public const string RecurringJobId = "jobs-outbox-drain";

    public async Task RunAsync()
    {
        try
        {
            await processor.DrainAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            // The drain already absorbs a failing handler. Reaching here means
            // the database itself refused, and letting it escape would have
            // Hangfire retry the whole batch with its own backoff — which is
            // fine, but silent. Logging first is what makes it visible.
            logger.LogError(exception, "The outbox drain failed");
            throw;
        }
    }
}
