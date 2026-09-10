namespace JobTracker.Common.Application;

public interface IIntegrationEventHandler<in TIntegrationEvent>
    where TIntegrationEvent : class
{
    Task HandleAsync(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
