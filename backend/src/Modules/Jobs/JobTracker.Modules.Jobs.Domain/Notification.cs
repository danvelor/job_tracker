using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed,
}

public sealed class Notification : Entity, ITenantScoped
{
    private Notification(Guid id) : base(id) { }

    private Notification() { }

    public Guid SourceEventId { get; private init; }

    public Guid OrganizationId { get; private init; }
    public string Recipient { get; private init; } = string.Empty;
    public string Subject { get; private init; } = string.Empty;
    public string Body { get; private init; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public string? FailureReason { get; private set; }

    public static Result<Notification> Draft(
        Guid sourceEventId,
        Guid organizationId,
        string recipient,
        string subject,
        string body,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return Result.Failure<Notification>(NotificationErrors.RecipientRequired);
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            return Result.Failure<Notification>(NotificationErrors.SubjectRequired);
        }

        return Result.Success(new Notification(Guid.NewGuid())
        {
            SourceEventId = sourceEventId,
            OrganizationId = organizationId,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = NotificationStatus.Pending,
            CreatedAt = now,
        });
    }

    public Result MarkSent(DateTimeOffset sentAt)
    {
        if (Status != NotificationStatus.Pending)
        {
            return Result.Failure(NotificationErrors.NotPending);
        }

        Status = NotificationStatus.Sent;
        SentAt = sentAt;

        return Result.Success();
    }

    public Result MarkFailed(string reason, DateTimeOffset failedAt)
    {
        if (Status != NotificationStatus.Pending)
        {
            return Result.Failure(NotificationErrors.NotPending);
        }

        Status = NotificationStatus.Failed;
        FailureReason = reason;
        FailedAt = failedAt;

        return Result.Success();
    }
}
