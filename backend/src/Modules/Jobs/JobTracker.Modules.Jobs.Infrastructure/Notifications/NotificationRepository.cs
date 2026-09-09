using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Modules.Jobs.Infrastructure.Notifications;

internal sealed class NotificationRepository(JobsDbContext context) : INotificationRepository
{
    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Notifications.SingleOrDefaultAsync(
            notification => notification.Id == id, cancellationToken);

    public async Task AddAsync(
        Notification notification, CancellationToken cancellationToken = default) =>
        await context.Notifications.AddAsync(notification, cancellationToken);

    public Task<bool> ExistsAsync(
        Guid sourceEventId, string recipient, CancellationToken cancellationToken = default) =>
        context.Notifications.AsNoTracking().AnyAsync(
            notification => notification.SourceEventId == sourceEventId
                            && notification.Recipient == recipient,
            cancellationToken);
}
