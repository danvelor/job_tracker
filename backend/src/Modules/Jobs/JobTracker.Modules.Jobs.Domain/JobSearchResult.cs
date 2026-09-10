namespace JobTracker.Modules.Jobs.Domain;

public sealed record JobSearchResult(
    Guid Id,
    string Title,
    JobStatus Status,
    DateOnly? ScheduledDate,
    Guid? AssigneeId,
    string? AssigneeName,
    string Street,
    string City,
    string State,
    int PhotoCount);

public enum JobSortField
{
    ScheduledDate,
    Title,
}

public sealed record JobSearchCriteria(
    Guid OrganizationId,
    string? Text,
    IReadOnlyList<JobStatus>? Statuses,
    DateOnly? From,
    DateOnly? To,
    Guid? AssigneeId,
    JobSortField Sort,
    string? Cursor,
    int Limit);
