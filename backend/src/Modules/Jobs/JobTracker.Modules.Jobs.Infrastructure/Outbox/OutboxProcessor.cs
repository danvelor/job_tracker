using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

/// <summary>
/// Architecture 4.4, role one. Every poll it takes a batch of unprocessed rows,
/// republishes each through MediatR, and stamps the ones that succeeded.
///
/// The batch is selected FOR UPDATE SKIP LOCKED inside an explicit transaction,
/// so several workers can poll at once and each row is handled by one of them.
/// Without it two workers read the same batch and every consequence happens
/// twice — which the consumers' unique constraints absorb, but at the cost of
/// doing all the work twice on every poll.
/// </summary>
public sealed class OutboxProcessor(
    JobsDbContext context,
    IPublisher publisher,
    TimeProvider time,
    IOptions<OutboxOptions> options)
{
    public async Task DrainAsync(CancellationToken cancellationToken)
    {
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        var batch = await context.OutboxMessages
            .FromSql($"""
                      select * from jobs.outbox_messages
                      where processed_on is null
                      order by occurred_on
                      limit {options.Value.BatchSize}
                      for update skip locked
                      """)
            .ToListAsync(cancellationToken);

        foreach (var message in batch)
        {
            await PublishOne(message, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task PublishOne(OutboxMessage message, CancellationToken cancellationToken)
    {
        // Caught per message, not per batch. A drain that stopped at the first
        // failure would stop every unrelated job in the system from billing or
        // notifying — this is the one place a broad catch is the right shape.
        try
        {
            await publisher.Publish(
                OutboxSerializer.Deserialize(message.Type, message.Content), cancellationToken);

            message.MarkProcessed(time.GetUtcNow());
        }
        catch (Exception exception)
        {
            // Deliberately left unprocessed: the next drain tries again, and
            // the error is a note for whoever reads the table.
            message.RecordFailure(exception.ToString());
        }
    }
}
