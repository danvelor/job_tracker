using FluentAssertions;

namespace JobTracker.Modules.Jobs.Domain.UnitTests;

/// <summary>
/// A record inside Jobs rather than a module (D-22). It has one lifecycle and
/// no other invariant, which is exactly why a module would be the anemic kind
/// D-04 gave Billing a real domain in order to avoid.
/// </summary>
public sealed class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid SourceEvent = Guid.NewGuid();
    private static readonly Guid Organization = Guid.NewGuid();

    private static Notification ADraft() =>
        Notification.Draft(
            SourceEvent, Organization, "J. Ortiz", "A job was assigned to you",
            "Ridge tile replacement on 2099-03-14", Now).Value;

    [Fact]
    public void A_drafted_notification_starts_Pending()
    {
        ADraft().Status.Should().Be(NotificationStatus.Pending);
    }

    [Fact]
    public void A_draft_records_the_event_it_came_from()
    {
        // The idempotency key (4.5). Without it a replayed message writes a
        // second row and the customer is told twice.
        ADraft().SourceEventId.Should().Be(SourceEvent);
    }

    [Fact]
    public void A_notification_without_a_recipient_is_refused()
    {
        Notification.Draft(SourceEvent, Organization, "  ", "subject", "body", Now)
            .Error.Should().Be(NotificationErrors.RecipientRequired);
    }

    [Fact]
    public void A_notification_without_a_subject_is_refused()
    {
        Notification.Draft(SourceEvent, Organization, "J. Ortiz", " ", "body", Now)
            .Error.Should().Be(NotificationErrors.SubjectRequired);
    }

    [Fact]
    public void Sending_moves_it_to_Sent_and_records_when()
    {
        var notification = ADraft();
        var sentAt = Now.AddSeconds(3);

        notification.MarkSent(sentAt).IsSuccess.Should().BeTrue();

        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.SentAt.Should().Be(sentAt);
    }

    [Fact]
    public void Failing_records_the_reason()
    {
        var notification = ADraft();

        notification.MarkFailed("the transport refused", Now).IsSuccess.Should().BeTrue();

        notification.Status.Should().Be(NotificationStatus.Failed);
        notification.FailureReason.Should().Be("the transport refused");
    }

    [Fact]
    public void A_sent_notification_refuses_every_further_transition()
    {
        var notification = ADraft();
        notification.MarkSent(Now);

        // The same terminal-state rule as BR-2. A retry that re-sent an
        // already-sent notification would tell the customer twice, which is
        // the outcome at-least-once delivery makes likely rather than rare.
        notification.MarkSent(Now.AddMinutes(1)).Error.Should().Be(NotificationErrors.NotPending);
        notification.MarkFailed("late", Now.AddMinutes(1))
            .Error.Should().Be(NotificationErrors.NotPending);
    }

    [Fact]
    public void A_failed_notification_refuses_every_further_transition()
    {
        var notification = ADraft();
        notification.MarkFailed("the transport refused", Now);

        notification.MarkSent(Now.AddMinutes(1)).Error.Should().Be(NotificationErrors.NotPending);
    }

    [Fact]
    public void A_refused_transition_changes_nothing()
    {
        var notification = ADraft();
        notification.MarkSent(Now);

        notification.MarkFailed("late", Now.AddMinutes(1));

        // Recording a failure on a notification that was sent would turn the
        // table into a worse record than no table.
        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.FailureReason.Should().BeNull();
    }
}
