namespace JobTracker.Modules.Jobs.Domain;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid sourceEventId, string recipient, CancellationToken cancellationToken = default);
}
