using JobTracker.Common.Application;
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Jobs.CreateJob;

internal sealed class CreateJobCommandHandler(
    IJobRepository jobs,
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

        // The handler supplies the instant; the aggregate never reads a clock.
        var job = Job.Create(
            command.Title, command.Description, address.Value,
            command.ScheduledDate, command.AssigneeId, command.CustomerId,
            command.OrganizationId, time.GetUtcNow());

        if (job.IsFailure)
        {
            return Result.Failure<Guid>(job.Error);
        }

        await jobs.AddAsync(job.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(job.Value.Id);
    }
}
