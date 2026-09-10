using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Modules.Jobs.Infrastructure.Repositories;

internal sealed partial class JobRepository
{
    public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Jobs
            .Include(job => job.Photos)
            .SingleOrDefaultAsync(job => job.Id == id, cancellationToken);

    public async Task<IReadOnlyList<JobSearchResult>> SearchAsync(
        JobSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = context.Jobs.AsNoTracking();

        if (criteria.Statuses is { Count: > 0 })
        {
            query = query.Where(job => criteria.Statuses.Contains(job.Status));
        }

        if (criteria.From is not null)
        {
            query = query.Where(job => job.ScheduledDate >= criteria.From);
        }

        if (criteria.To is not null)
        {
            query = query.Where(job => job.ScheduledDate <= criteria.To);
        }

        if (criteria.AssigneeId is not null)
        {
            query = query.Where(job => job.AssigneeId == criteria.AssigneeId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Text))
        {
            query = query.Where(job =>
                EF.Functions
                    .ToTsVector("english", job.Title + " " + (job.Description ?? string.Empty))
                    .Matches(EF.Functions.WebSearchToTsQuery("english", criteria.Text!)));
        }

        query = await ApplyCursorAsync(query, criteria, cancellationToken);

        var ordered = criteria.Sort == JobSortField.Title
            ? query.OrderBy(job => job.Title).ThenBy(job => job.Id)
            : query.OrderByDescending(job => job.ScheduledDate ?? EF.Constant(DateOnly.MinValue))
                .ThenByDescending(job => job.Id);

        return await ordered
            .Take(criteria.Limit)
            .Select(job => new JobSearchResult(
                job.Id,
                job.Title,
                job.Status,
                job.ScheduledDate,
                job.AssigneeId,
                context.Assignees
                    .Where(assignee => assignee.Id == job.AssigneeId)
                    .Select(assignee => assignee.Name)
                    .FirstOrDefault(),
                job.Address.Street,
                job.Address.City,
                job.Address.State,
                job.Photos.Count))
            .ToListAsync(cancellationToken);
    }

    private async Task<IQueryable<Job>> ApplyCursorAsync(
        IQueryable<Job> query, JobSearchCriteria criteria, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(criteria.Cursor, out var cursorId))
        {
            return query;
        }

        if (criteria.Sort == JobSortField.Title)
        {
            var titleAnchor = await context.Jobs.AsNoTracking()
                .Where(job => job.Id == cursorId)
                .Select(job => new { job.Title, job.Id })
                .FirstOrDefaultAsync(cancellationToken);

            return titleAnchor is null
                ? query
                : query.Where(job =>
                    string.Compare(job.Title, titleAnchor.Title) > 0
                    || (job.Title == titleAnchor.Title && job.Id > titleAnchor.Id));
        }

        var anchor = await context.Jobs.AsNoTracking()
            .Where(job => job.Id == cursorId)
            .Select(job => new { Date = job.ScheduledDate ?? EF.Constant(DateOnly.MinValue), job.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (anchor is null)
        {
            return query;
        }

        return query.Where(job =>
            (job.ScheduledDate ?? EF.Constant(DateOnly.MinValue)) < anchor.Date
            || ((job.ScheduledDate ?? EF.Constant(DateOnly.MinValue)) == anchor.Date
                && job.Id < anchor.Id));
    }
}
