namespace JobTracker.Modules.Jobs.Domain;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// The read behind idempotency. It exists so a handler can absorb a replay
    /// quietly instead of letting the unique constraint throw — a violation
    /// that escaped would leave the outbox row unprocessed and the drain would
    /// retry it every poll forever.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid sourceEventId, string recipient, CancellationToken cancellationToken = default);
}
