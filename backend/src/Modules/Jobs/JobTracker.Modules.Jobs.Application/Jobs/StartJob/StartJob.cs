using FluentValidation;
using JobTracker.Common.Application;
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Jobs.StartJob;

public sealed record StartJobCommand(Guid JobId, Guid OrganizationId) : IRequest<Result>;

/// <summary>
/// No validator: the command carries only identifiers, and BR-3 — that only a
/// Scheduled job can start — is the aggregate's to enforce. A validator here
/// would be a second place to change the rule.
/// </summary>
internal sealed class StartJobCommandHandler(
    IJobRepository jobs,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IRequestHandler<StartJobCommand, Result>
{
    public async Task<Result> Handle(StartJobCommand command, CancellationToken cancellationToken)
    {
        var job = await jobs.GetByIdAsync(command.JobId, cancellationToken);
        if (job is null)
        {
            return Result.Failure(JobErrors.NotFound);
        }

        var started = job.Start(time.GetUtcNow());
        if (started.IsFailure)
        {
            // Nothing changed, so nothing is saved: a SaveChanges here would
            // write an unchanged aggregate and drain an outbox with no event.
            return started;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class StartJobCommandValidator : AbstractValidator<StartJobCommand>
{
    public StartJobCommandValidator()
    {
        RuleFor(command => command.JobId).NotEmpty();
        RuleFor(command => command.OrganizationId).NotEmpty();
    }
}
