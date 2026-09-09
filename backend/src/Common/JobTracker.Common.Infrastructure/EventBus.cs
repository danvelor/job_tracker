using JobTracker.Common.Application;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.Common.Infrastructure;

/// <summary>
/// Resolves every handler registered for the contract and calls them in turn.
/// Both modules share a process today (D-03), so the bus is a service lookup;
/// this class is the only place that is true, which is what makes swapping in a
/// broker a change here and nowhere else.
/// </summary>
public sealed class EventBus(IServiceProvider services) : IEventBus
{
    public async Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class
    {
        foreach (var handler in services.GetServices<IIntegrationEventHandler<T>>())
        {
            // Not caught here. A consumer that throws must leave the outbox row
            // unprocessed so the drain retries it — swallowing the failure
            // would turn at-least-once into at-most-once for that consumer.
            await handler.HandleAsync(integrationEvent, cancellationToken);
        }
    }
}
