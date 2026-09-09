using JobTracker.Modules.Jobs.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Modules.Jobs.Infrastructure.Repositories;

internal sealed partial class JobRepository
{
    /// <summary>
    /// Tracked on purpose: this loads the aggregate a command is about to
    /// change, and the change tracker is how the unit of work learns what to
    /// write. <see cref="SearchAsync"/> below is the untracked one.
    /// </summary>
    public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Jobs
            .Include(job => job.Photos)
            .SingleOrDefaultAsync(job => job.Id == id, cancellationToken);

    public async Task<IReadOnlyList<JobSearchResult>> SearchAsync(
        JobSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        // No Where on OrganizationId anywhere below. The global query filter
        // applies it, which is what makes NFR-1 independent of this method
        // remembering — see TenantIsolationTests.
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
            // A filter, never a sort. Ordering by ts_rank would make PostgreSQL
            // compute and sort the whole matched set for every page, which is
            // exactly the cost profile keyset pagination exists to remove
            // (D-12).
            query = query.Where(job =>
                EF.Functions
                    .ToTsVector("english", job.Title + " " + (job.Description ?? string.Empty))
                    .Matches(EF.Functions.WebSearchToTsQuery("english", criteria.Text!)));
        }

        query = await ApplyCursorAsync(query, criteria, cancellationToken);

        return await query
            // coalesce, not the bare column. Over a bare column the keyset
            // comparison yields NULL for a dateless row rather than true, WHERE
            // discards it, and the job disappears after page one
            // (architecture 6.4). Npgsql maps DateOnly.MinValue to
            // '-infinity', which sorts last under DESC — where an undated job
            // belongs in a schedule.
            //
            // EF.Constant, not the bare field: without it EF emits
            // COALESCE(scheduled_date, $1) and the planner cannot match a bind
            // parameter against the expression the index is declared over. With
            // it the SQL reads COALESCE(scheduled_date, DATE '-infinity'), which
            // is exactly ix_jobs_schedule_keyset.
            .OrderByDescending(job => job.ScheduledDate ?? EF.Constant(DateOnly.MinValue))
            .ThenByDescending(job => job.Id)
            .Take(criteria.Limit)
            .Select(job => new JobSearchResult(
                job.Id,
                job.Title,
                job.Status,
                job.ScheduledDate,
                job.AssigneeId,
                // A correlated subquery rather than a join: the roster is
                // filtered by tenant too, and a projection keeps the read side
                // free of a navigation the aggregate does not have.
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

    /// <summary>
    /// Resolves the cursor to the row it names and asks for everything strictly
    /// beyond it. Comparing the ordered pair rather than the date alone is what
    /// stops a row being skipped or served twice when two jobs share a date.
    ///
    /// An unparseable or unknown cursor returns the first page rather than an
    /// error: a cursor is a position, and a position that no longer exists is
    /// not a client mistake worth a 400.
    /// </summary>
    private async Task<IQueryable<Job>> ApplyCursorAsync(
        IQueryable<Job> query, JobSearchCriteria criteria, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(criteria.Cursor, out var cursorId))
        {
            return query;
        }

        var anchor = await context.Jobs.AsNoTracking()
            .Where(job => job.Id == cursorId)
            .Select(job => new { Date = job.ScheduledDate ?? EF.Constant(DateOnly.MinValue), job.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (anchor is null)
        {
            return query;
        }

        // The same expression as the ORDER BY, for the same reason: the
        // predicate has to be the one the index is built on.
        return query.Where(job =>
            (job.ScheduledDate ?? EF.Constant(DateOnly.MinValue)) < anchor.Date
            || ((job.ScheduledDate ?? EF.Constant(DateOnly.MinValue)) == anchor.Date
                && job.Id < anchor.Id));
    }
}
