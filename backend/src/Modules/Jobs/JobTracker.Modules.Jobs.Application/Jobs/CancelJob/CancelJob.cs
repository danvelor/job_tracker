using FluentValidation;
using JobTracker.Common.Application;
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Jobs.CancelJob;

public sealed record CancelJobCommand(Guid JobId, Guid OrganizationId, string Reason)
    : IRequest<Result>;

internal sealed class CancelJobCommandHandler(
    IJobRepository jobs,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IRequestHandler<CancelJobCommand, Result>
{
    public async Task<Result> Handle(CancelJobCommand command, CancellationToken cancellationToken)
    {
        var job = await jobs.GetByIdAsync(command.JobId, cancellationToken);
        if (job is null)
        {
            return Result.Failure(JobErrors.NotFound);
        }

        var cancelled = job.Cancel(time.GetUtcNow(), command.Reason);
        if (cancelled.IsFailure)
        {
            return cancelled;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class CancelJobCommandValidator : AbstractValidator<CancelJobCommand>
{
    public CancelJobCommandValidator()
    {
        RuleFor(command => command.JobId).NotEmpty();
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty();
    }
}
