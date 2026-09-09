using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

/// <summary>
/// One error per business rule, named after the rule it enforces.
///
/// A refusal about an input names the input. Design A5 point 3 wants the field
/// that failed to light up rather than a banner, and the aggregate is what
/// knows which value was wrong — so the field name lives here, next to the
/// rule, in the same way the error code already does. A refusal about the
/// job's state names nothing, because nothing the caller sent was wrong.
/// </summary>
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

    /// <summary>BR-1.</summary>
    public static readonly Error ScheduledInThePast = Error.Validation(
        "job.scheduled-in-the-past",
        "A job cannot be scheduled in the past",
        On("ScheduledDate", "A job cannot be scheduled in the past"));

    /// <summary>BR-2. About the job, not the request.</summary>
    public static readonly Error Terminal =
        Error.Conflict("job.terminal", "A job in a terminal state cannot change state");

    /// <summary>BR-3. About the job, not the request.</summary>
    public static readonly Error NotScheduled =
        Error.Conflict("job.not-scheduled", "Only a Scheduled job can start");

    public static readonly Error NotInProgress =
        Error.Conflict("job.not-in-progress", "Only a job in progress can be completed");

    /// <summary>BR-4.</summary>
    public static readonly Error SignatureRequired = Error.Validation(
        "job.signature-required",
        "A customer signature is required",
        On("SignatureUrl", "A customer signature is required"));

    /// <summary>BR-5.</summary>
    public static readonly Error ReasonRequired = Error.Validation(
        "job.reason-required",
        "A cancellation reason is required",
        On("Reason", "A cancellation reason is required"));

    public static readonly Error NotFound =
        Error.NotFound("job.not-found", "No job with that identifier");
}
