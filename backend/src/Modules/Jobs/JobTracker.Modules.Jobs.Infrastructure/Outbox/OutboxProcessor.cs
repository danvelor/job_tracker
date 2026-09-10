using JobTracker.Common.Infrastructure;
using JobTracker.Modules.Jobs.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

public sealed class OutboxProcessor(
    JobsDbContext context,
    IPublisher publisher,
    ITenantContextSetter tenant,
    IBackgroundQueue queue,
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

        queue.Flush();
    }

    private async Task PublishOne(OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var domainEvent = OutboxSerializer.Deserialize(message.Type, message.Content);

            using (tenant.Use(domainEvent.OrganizationId))
            {
                await publisher.Publish(domainEvent, cancellationToken);
            }

            message.MarkProcessed(time.GetUtcNow());
        }
        catch (Exception exception)
        {
            message.RecordFailure(exception.ToString());
        }
    }
}
