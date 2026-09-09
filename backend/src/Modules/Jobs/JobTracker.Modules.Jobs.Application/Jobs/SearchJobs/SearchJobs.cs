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
        // One row more than the page. That extra row is how the handler learns
        // another page exists without asking for a count, which is the cost
        // NFR-5 rejects.
        var criteria = new JobSearchCriteria(
            query.OrganizationId, query.Text, query.Statuses, query.From, query.To,
            query.AssigneeId, query.Sort, query.Cursor, query.Limit + 1);

        var rows = await jobs.SearchAsync(criteria, cancellationToken);

        var hasMore = rows.Count > query.Limit;
        var page = hasMore ? rows.Take(query.Limit).ToList() : rows;

        // The repository runs the query; the handler builds the envelope (D-25).
        return Result.Success(new PagedList<JobResponse>(
            page.Select(Map).ToList(),
            hasMore && page.Count > 0 ? page[^1].Id.ToString() : null));
    }

    private static JobResponse Map(JobSearchResult row) => new(
        row.Id,
        row.Title,
        // Text, not an ordinal: an ordinal on the wire is what makes an enum
        // dangerous across deployments, and the schema stores text for the
        // same reason (architecture 6.2).
        row.Status.ToString(),
        row.ScheduledDate,
        row.AssigneeId,
        row.AssigneeName,
        row.Street,
        row.City,
        row.State,
        row.PhotoCount);
}
