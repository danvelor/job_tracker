using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

/// <summary>One error per business rule, named after the rule it enforces.</summary>
public static class JobErrors
{
    public static readonly Error TitleRequired =
        Error.Validation("job.title-required", "A title is required");

    public static readonly Error AddressIncomplete =
        Error.Validation("job.address-incomplete", "Every address component is required");

    public static readonly Error CoordinatesOffGlobe =
        Error.Validation("job.coordinates-off-globe", "The coordinates are not a place on Earth");

    /// <summary>BR-1.</summary>
    public static readonly Error ScheduledInThePast =
        Error.Validation("job.scheduled-in-the-past", "A job cannot be scheduled in the past");

    /// <summary>BR-2.</summary>
    public static readonly Error Terminal =
        Error.Conflict("job.terminal", "A job in a terminal state cannot change state");

    /// <summary>BR-3.</summary>
    public static readonly Error NotScheduled =
        Error.Conflict("job.not-scheduled", "Only a Scheduled job can start");

    public static readonly Error NotInProgress =
        Error.Conflict("job.not-in-progress", "Only a job in progress can be completed");

    /// <summary>BR-4.</summary>
    public static readonly Error SignatureRequired =
        Error.Validation("job.signature-required", "A customer signature is required");

    /// <summary>BR-5.</summary>
    public static readonly Error ReasonRequired =
        Error.Validation("job.reason-required", "A cancellation reason is required");

    public static readonly Error NotFound =
        Error.NotFound("job.not-found", "No job with that identifier");
}
