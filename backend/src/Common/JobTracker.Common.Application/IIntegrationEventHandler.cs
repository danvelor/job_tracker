namespace JobTracker.Common.Application;

/// <summary>
/// What a consumer of a published contract implements.
///
/// Deliberately not <c>MediatR.INotificationHandler</c>. A contract project
/// references nothing — that is what makes it unable to leak anything — so the
/// contract cannot be an <c>INotification</c>, and forcing it to be one would
/// put MediatR's version in every consumer's dependency graph. This interface
/// is the seam D-03 describes: replacing the in-process bus with a broker
/// changes <see cref="IEventBus"/> and this resolution, and no handler.
/// </summary>
public interface IIntegrationEventHandler<in TIntegrationEvent>
    where TIntegrationEvent : class
{
    Task HandleAsync(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
