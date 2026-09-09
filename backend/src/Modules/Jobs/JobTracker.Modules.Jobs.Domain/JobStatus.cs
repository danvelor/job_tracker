namespace JobTracker.Modules.Jobs.Domain;

public enum JobStatus
{
    Draft,
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
}
