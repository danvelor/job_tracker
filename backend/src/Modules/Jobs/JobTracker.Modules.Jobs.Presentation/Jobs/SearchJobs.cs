using JobTracker.Common.Infrastructure;
using JobTracker.Common.Presentation;
using JobTracker.Modules.Jobs.Application.Jobs.SearchJobs;
using JobTracker.Modules.Jobs.Domain;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace JobTracker.Modules.Jobs.Presentation.Jobs;

internal sealed class SearchJobs : IEndpoint
{
    /// <summary>The one endpoint with parsing to do, and the only place it belongs.</summary>
    internal sealed record Query(
        [FromQuery] string? Text,
        [FromQuery] string[]? Statuses,
        [FromQuery] DateOnly? ScheduledFrom,
        [FromQuery] DateOnly? ScheduledTo,
        [FromQuery] Guid? AssigneeId,
        [FromQuery] string? Sort,
        [FromQuery] string? Cursor,
        [FromQuery] int? Limit);

    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/api/jobs", async (
                [AsParameters] Query query,
                ITenantContext tenant,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!TryParseStatuses(query.Statuses, out var statuses))
                {
                    // A typo is a contract violation, not an empty result set.
                    // An empty list would read as "no jobs match", which is a
                    // different and wrong answer.
                    return Results.Problem(
                        title: "Unknown status",
                        detail: "Every value of `statuses` must be a defined job status.",
                        statusCode: StatusCodes.Status400BadRequest,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = "query.statuses" });
                }

                var result = await sender.Send(
                    new SearchJobsQuery(
                        tenant.OrganizationId,
                        query.Text,
                        statuses,
                        query.ScheduledFrom,
                        query.ScheduledTo,
                        query.AssigneeId,
                        query.Sort == "title" ? JobSortField.Title : JobSortField.ScheduledDate,
                        query.Cursor,
                        // A caller asking for ten thousand rows gets fifty.
                        // NFR-5 is not enforceable if the page size is not.
                        Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit)),
                    cancellationToken);

                return result.Match(Results.Ok);
            })
            .WithTags("Jobs");

    private static bool TryParseStatuses(
        string[]? raw, out IReadOnlyList<JobStatus>? statuses)
    {
        statuses = null;

        if (raw is null || raw.Length == 0)
        {
            return true;
        }

        var parsed = new List<JobStatus>(raw.Length);

        foreach (var value in raw)
        {
            // Case-sensitive on purpose: the wire format is the enum's name,
            // and accepting "completed" here while the schema stores
            // "Completed" would make two spellings of one thing.
            if (!Enum.TryParse<JobStatus>(value, ignoreCase: false, out var status))
            {
                return false;
            }

            parsed.Add(status);
        }

        statuses = parsed;
        return true;
    }
}
