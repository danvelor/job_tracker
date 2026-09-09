using FluentValidation;
using JobTracker.Common.Application;
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Jobs.CompleteJob;

public sealed record NewPhotoInput(string Url, string? Caption);

public sealed record CompleteJobCommand(
    Guid JobId,
    Guid OrganizationId,
    string SignatureUrl,
    IReadOnlyList<NewPhotoInput> Photos) : IRequest<Result>;

internal sealed class CompleteJobCommandHandler(
    IJobRepository jobs,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IRequestHandler<CompleteJobCommand, Result>
{
    public async Task<Result> Handle(
        CompleteJobCommand command, CancellationToken cancellationToken)
    {
        var job = await jobs.GetByIdAsync(command.JobId, cancellationToken);
        if (job is null)
        {
            return Result.Failure(JobErrors.NotFound);
        }

        var now = time.GetUtcNow();

        // The capture time comes from the clock rather than from the request:
        // a client-supplied timestamp is evidence the client controls.
        var photos = command.Photos
            .Select(photo => new NewJobPhoto(photo.Url, now, photo.Caption))
            .ToList();

        var completed = job.Complete(now, command.SignatureUrl, photos);
        if (completed.IsFailure)
        {
            return completed;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// Shape only. BR-4 lives in the aggregate; this rejects a request that could
/// not possibly satisfy it before a database round trip.
/// </summary>
internal sealed class CompleteJobCommandValidator : AbstractValidator<CompleteJobCommand>
{
    public CompleteJobCommandValidator()
    {
        RuleFor(command => command.JobId).NotEmpty();
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.SignatureUrl).NotEmpty();
    }
}
