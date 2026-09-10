using JobTracker.Common.Application;
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Jobs.SearchJobs;

public sealed record JobResponse(
    Guid Id,
    string Title,
    string Status,
    DateOnly? ScheduledDate,
    Guid? AssigneeId,
    string? AssigneeName,
    string Street,
    string City,
    string State,
    int PhotoCount);

public sealed record SearchJobsQuery(
    Guid OrganizationId,
    string? Text,
    IReadOnlyList<JobStatus>? Statuses,
    DateOnly? From,
    DateOnly? To,
    Guid? AssigneeId,
    JobSortField Sort,
    string? Cursor,
    int Limit) : IRequest<Result<PagedList<JobResponse>>>;

internal sealed class SearchJobsQueryHandler(IJobRepository jobs)
    : IRequestHandler<SearchJobsQuery, Result<PagedList<JobResponse>>>
{
    public async Task<Result<PagedList<JobResponse>>> Handle(
        SearchJobsQuery query, CancellationToken cancellationToken)
    {
        var criteria = new JobSearchCriteria(
            query.OrganizationId, query.Text, query.Statuses, query.From, query.To,
            query.AssigneeId, query.Sort, query.Cursor, query.Limit + 1);

        var rows = await jobs.SearchAsync(criteria, cancellationToken);

        var hasMore = rows.Count > query.Limit;
        var page = hasMore ? rows.Take(query.Limit).ToList() : rows;

        return Result.Success(new PagedList<JobResponse>(
            page.Select(Map).ToList(),
            hasMore && page.Count > 0 ? page[^1].Id.ToString() : null));
    }

    private static JobResponse Map(JobSearchResult row) => new(
        row.Id,
        row.Title,
        row.Status.ToString(),
        row.ScheduledDate,
        row.AssigneeId,
        row.AssigneeName,
        row.Street,
        row.City,
        row.State,
        row.PhotoCount);
}
