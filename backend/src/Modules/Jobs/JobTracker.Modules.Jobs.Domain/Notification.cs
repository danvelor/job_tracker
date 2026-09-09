using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed,
}

/// <summary>
/// A record inside Jobs rather than a module of its own (D-22). Notifying has
/// one lifecycle and no other invariant, so a module would be the anemic kind
/// D-04 gave Billing a real domain in order to avoid.
///
/// Delivery is simulated; the record is not. A reviewer verifies FR-8 and FR-10
/// with <c>select status, recipient from jobs.notifications</c>, which is
/// stronger evidence than a log line and is what makes NFR-3 checkable in the
/// data.
/// </summary>
public sealed class Notification : Entity, ITenantScoped
{
    private Notification(Guid id) : base(id) { }

    // EF only.
    private Notification() { }

    /// <summary>
    /// The identity of the domain event that caused this (D-34). Half of the
    /// idempotency key, and stable across a replay — which is the condition
    /// architecture 4.5 puts on one.
    /// </summary>
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

    /// <summary>
    /// The same terminal-state rule as BR-2. A retry that re-sent an
    /// already-sent notification would tell the customer twice — and
    /// at-least-once delivery makes that likely rather than rare.
    /// </summary>
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
