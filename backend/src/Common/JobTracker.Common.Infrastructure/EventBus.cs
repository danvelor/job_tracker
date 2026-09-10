using JobTracker.Common.Application;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.Common.Infrastructure;

public sealed class EventBus(IServiceProvider services) : IEventBus
{
    public async Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class
    {
        foreach (var handler in services.GetServices<IIntegrationEventHandler<T>>())
        {
            await handler.HandleAsync(integrationEvent, cancellationToken);
        }
    }
}
