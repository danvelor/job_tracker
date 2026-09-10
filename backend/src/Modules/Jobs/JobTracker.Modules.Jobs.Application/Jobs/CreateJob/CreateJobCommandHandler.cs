using JobTracker.Common.Application;
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Jobs.CreateJob;

internal sealed class CreateJobCommandHandler(
    IJobRepository jobs,
    IPartyRepository parties,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IRequestHandler<CreateJobCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateJobCommand command, CancellationToken cancellationToken)
    {
        var address = Address.Create(
            command.Street, command.City, command.State,
            command.ZipCode, command.Latitude, command.Longitude);

        if (address.IsFailure)
        {
            return Result.Failure<Guid>(address.Error);
        }

        var job = Job.Create(
            command.Title, command.Description, address.Value,
            command.ScheduledDate, command.AssigneeId, command.CustomerId,
            command.OrganizationId, time.GetUtcNow());

        if (job.IsFailure)
        {
            return Result.Failure<Guid>(job.Error);
        }

        var onTheRoster = await BothPartiesAreOnTheRoster(command, cancellationToken);
        if (onTheRoster.IsFailure)
        {
            return Result.Failure<Guid>(onTheRoster.Error);
        }

        await jobs.AddAsync(job.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(job.Value.Id);
    }

    private async Task<Result> BothPartiesAreOnTheRoster(
        CreateJobCommand command, CancellationToken cancellationToken)
    {
        if (!await parties.AssigneeExistsAsync(
                command.AssigneeId, command.OrganizationId, cancellationToken))
        {
            return Result.Failure(JobErrors.AssigneeNotOnTheRoster);
        }

        if (!await parties.CustomerExistsAsync(
                command.CustomerId, command.OrganizationId, cancellationToken))
        {
            return Result.Failure(JobErrors.CustomerNotOnTheRoster);
        }

        return Result.Success();
    }
}
