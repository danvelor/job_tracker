namespace JobTracker.Modules.Jobs.Domain;

/// <summary>A read model. Not an aggregate: no identity, no behaviour (D-25).</summary>
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

/// <summary>A Specification: the query expressed in domain terms.</summary>
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
