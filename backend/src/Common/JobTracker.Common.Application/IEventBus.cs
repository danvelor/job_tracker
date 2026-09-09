namespace JobTracker.Common.Application;

/// <summary>
/// Publishes integration events. It exists so a module publishing a contract
/// does not name the mediator, which is the piece that would change if the
/// modules ever became separate deployables (D-03).
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class;
}
