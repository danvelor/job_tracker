using JobTracker.Common.Application;
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Application.Abstractions;
using JobTracker.Modules.Jobs.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Notifications;

public sealed record SendNotificationCommand(Guid NotificationId) : IRequest<Result>;

internal sealed class SendNotificationCommandHandler(
    INotificationRepository notifications,
    INotificationSender sender,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IRequestHandler<SendNotificationCommand, Result>
{
    public async Task<Result> Handle(
        SendNotificationCommand command, CancellationToken cancellationToken)
    {
        var notification = await notifications.GetByIdAsync(command.NotificationId, cancellationToken);
        if (notification is null)
        {
            return Result.Failure(NotificationErrors.NotFound);
        }

        if (notification.Status != NotificationStatus.Pending)
        {
            return Result.Success();
        }

        var outcome = await sender.SendAsync(
            notification.Recipient, notification.Subject, notification.Body, cancellationToken);

        var result = outcome.Succeeded
            ? notification.MarkSent(time.GetUtcNow())
            : notification.MarkFailed(outcome.Reason ?? "unknown", time.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return result;
    }
}
