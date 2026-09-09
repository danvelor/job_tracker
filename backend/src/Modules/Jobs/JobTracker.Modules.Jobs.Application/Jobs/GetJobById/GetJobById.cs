using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Jobs.GetJobById;

public sealed record JobPhotoResponse(Guid Id, string Url, DateTimeOffset CapturedAt, string? Caption);

public sealed record JobDetailResponse(
    Guid Id,
    string Title,
    string? Description,
    string Status,
    DateOnly? ScheduledDate,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    string? SignatureUrl,
    Guid? AssigneeId,
    Guid CustomerId,
    string Street,
    string City,
    string State,
    string ZipCode,
    decimal Latitude,
    decimal Longitude,
    IReadOnlyList<JobPhotoResponse> Photos);

public sealed record GetJobByIdQuery(Guid JobId, Guid OrganizationId)
    : IRequest<Result<JobDetailResponse>>;

internal sealed class GetJobByIdQueryHandler(IJobRepository jobs)
    : IRequestHandler<GetJobByIdQuery, Result<JobDetailResponse>>
{
    public async Task<Result<JobDetailResponse>> Handle(
        GetJobByIdQuery query, CancellationToken cancellationToken)
    {
        var job = await jobs.GetByIdAsync(query.JobId, cancellationToken);

        return job is null
            ? Result.Failure<JobDetailResponse>(JobErrors.NotFound)
            : Result.Success(Map(job));
    }

    private static JobDetailResponse Map(Job job) => new(
        job.Id, job.Title, job.Description, job.Status.ToString(), job.ScheduledDate,
        job.StartedAt, job.CompletedAt, job.CancelledAt, job.CancellationReason,
        job.SignatureUrl, job.AssigneeId, job.CustomerId,
        job.Address.Street, job.Address.City, job.Address.State, job.Address.ZipCode,
        job.Address.Latitude, job.Address.Longitude,
        job.Photos
            .Select(photo => new JobPhotoResponse(photo.Id, photo.Url, photo.CapturedAt, photo.Caption))
            .ToList());
}
