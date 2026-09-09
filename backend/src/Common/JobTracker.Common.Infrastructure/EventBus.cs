using JobTracker.Common.Application;
using MediatR;

namespace JobTracker.Common.Infrastructure;

/// <summary>
/// Integration events go through MediatR today because both modules share a
/// process (D-03). This class is the only place that is true, which is what
/// makes swapping in a broker a change here and nowhere else.
/// </summary>
public sealed class EventBus(IPublisher publisher) : IEventBus
{
    public Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class =>
        publisher.Publish(integrationEvent, cancellationToken);
}
