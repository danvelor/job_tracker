using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

public static class JobErrors
{
    private static IReadOnlyDictionary<string, string[]> On(string field, string message) =>
        new Dictionary<string, string[]> { [field] = [message] };

    public static readonly Error TitleRequired = Error.Validation(
        "job.title-required", "A title is required", On("Title", "A title is required"));

    public static readonly Error AddressIncomplete = Error.Validation(
        "job.address-incomplete",
        "Every address component is required",
        On("Street", "Every address component is required"));

    public static readonly Error CoordinatesOffGlobe = Error.Validation(
        "job.coordinates-off-globe",
        "The coordinates are not a place on Earth",
        On("Latitude", "The coordinates are not a place on Earth"));

    public static readonly Error ScheduledInThePast = Error.Validation(
        "job.scheduled-in-the-past",
        "A job cannot be scheduled in the past",
        On("ScheduledDate", "A job cannot be scheduled in the past"));

    public static readonly Error Terminal =
        Error.Conflict("job.terminal", "A job in a terminal state cannot change state");

    public static readonly Error NotScheduled =
        Error.Conflict("job.not-scheduled", "Only a Scheduled job can start");

    public static readonly Error NotInProgress =
        Error.Conflict("job.not-in-progress", "Only a job in progress can be completed");

    public static readonly Error SignatureRequired = Error.Validation(
        "job.signature-required",
        "A customer signature is required",
        On("SignatureUrl", "A customer signature is required"));

    public static readonly Error ReasonRequired = Error.Validation(
        "job.reason-required",
        "A cancellation reason is required",
        On("Reason", "A cancellation reason is required"));

    public static readonly Error AssigneeNotOnTheRoster = Error.Validation(
        "job.assignee-not-on-the-roster",
        "That crew member is not on this organization's roster",
        On("AssigneeId", "That crew member is not on this organization's roster"));

    public static readonly Error CustomerNotOnTheRoster = Error.Validation(
        "job.customer-not-on-the-roster",
        "That customer is not on this organization's roster",
        On("CustomerId", "That customer is not on this organization's roster"));

    public static readonly Error NotFound =
        Error.NotFound("job.not-found", "No job with that identifier");
}
