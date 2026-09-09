using JobTracker.Common.Domain;
using MediatR;

namespace JobTracker.Modules.Jobs.Application.Jobs.CreateJob;

public sealed record CreateJobCommand(
    string Title,
    string? Description,
    string Street,
    string City,
    string State,
    string ZipCode,
    decimal Latitude,
    decimal Longitude,
    DateOnly ScheduledDate,
    Guid AssigneeId,
    Guid CustomerId,
    Guid OrganizationId) : IRequest<Result<Guid>>;
