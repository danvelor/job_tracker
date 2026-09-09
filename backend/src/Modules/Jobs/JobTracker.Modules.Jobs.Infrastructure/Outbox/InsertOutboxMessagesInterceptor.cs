using JobTracker.Common.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

/// <summary>
/// Architecture 4.2. It runs inside <c>SaveChangesAsync</c>, so the outbox rows
/// and the state change commit or roll back together — there is no window in
/// which a job is completed and its consequences are absent, and none in which
/// consequences are recorded for a completion that rolled back.
///
/// It persists the <em>domain</em> event rather than the integration event, and
/// that is a deliberate deviation from assessment lines 236-238. The integration
/// event is produced by a handler that runs after the domain event is published,
/// which is after the commit; an interceptor cannot serialise something that
/// does not exist yet. Translating downstream is the ordering that actually
/// holds the transactional guarantee (D-13).
/// </summary>
internal sealed class InsertOutboxMessagesInterceptor : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            AddOutboxMessages(eventData.Context);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void AddOutboxMessages(DbContext context)
    {
        var aggregates = context.ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        var messages = aggregates
            .SelectMany(aggregate => aggregate.DomainEvents)
            .Select(domainEvent => new OutboxMessage(
                domainEvent.Id,
                OutboxSerializer.NameOf(domainEvent),
                OutboxSerializer.Serialize(domainEvent),
                domainEvent.OccurredOn))
            .ToList();

        // Cleared after copying and before the write returns. Left in place, a
        // second SaveChanges in the same request would enqueue them again and
        // the crew would be notified twice about one job.
        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        context.Set<OutboxMessage>().AddRange(messages);
    }
}
