using FluentValidation;
using JobTracker.Common.Application;
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Jobs.RescheduleJob;

public sealed record RescheduleJobCommand(
    Guid JobId, Guid OrganizationId, DateOnly ScheduledDate, Guid AssigneeId) : IRequest<Result>;

internal sealed class RescheduleJobCommandHandler(
    IJobRepository jobs,
    IPartyRepository parties,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IRequestHandler<RescheduleJobCommand, Result>
{
    public async Task<Result> Handle(
        RescheduleJobCommand command, CancellationToken cancellationToken)
    {
        var job = await jobs.GetByIdAsync(command.JobId, cancellationToken);
        if (job is null)
        {
            return Result.Failure(JobErrors.NotFound);
        }

        var rescheduled = job.Reschedule(command.ScheduledDate, command.AssigneeId, time.GetUtcNow());
        if (rescheduled.IsFailure)
        {
            return rescheduled;
        }

        // Reassignment is the other door into the same hole: correcting a job
        // onto another organization's crew is the same forgery as creating one
        // there.
        if (!await parties.AssigneeExistsAsync(
                command.AssigneeId, command.OrganizationId, cancellationToken))
        {
            return Result.Failure(JobErrors.AssigneeNotOnTheRoster);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class RescheduleJobCommandValidator : AbstractValidator<RescheduleJobCommand>
{
    public RescheduleJobCommandValidator()
    {
        RuleFor(command => command.JobId).NotEmpty();
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.AssigneeId).NotEmpty();
    }
}
