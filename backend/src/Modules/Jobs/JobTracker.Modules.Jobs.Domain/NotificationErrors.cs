using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

public static class NotificationErrors
{
    public static readonly Error RecipientRequired = Error.Validation(
        "notification.recipient-required", "A notification needs somewhere to go");

    public static readonly Error SubjectRequired = Error.Validation(
        "notification.subject-required", "A notification needs a subject");

    public static readonly Error NotFound = Error.NotFound(
        "notification.not-found", "No notification with that identifier");

    public static readonly Error NotPending = Error.Conflict(
        "notification.not-pending", "Only a pending notification can be resolved");
}
