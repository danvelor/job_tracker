using Microsoft.Extensions.Logging;

namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

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
            logger.LogError(exception, "The outbox drain failed");
            throw;
        }
    }
}
