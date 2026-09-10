using JobTracker.Common.Infrastructure;
using JobTracker.Common.Presentation;
using JobTracker.Modules.Jobs.Application.Jobs.CreateJob;
using MediatR;

namespace JobTracker.Modules.Jobs.Presentation.Jobs;

internal sealed class CreateJob : IEndpoint
{
    internal sealed record Request(
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
        Guid CustomerId);

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/api/jobs", async (
                Request request,
                ITenantContext tenant,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CreateJobCommand(
                        request.Title, request.Description,
                        request.Street, request.City, request.State, request.ZipCode,
                        request.Latitude, request.Longitude,
                        request.ScheduledDate, request.AssigneeId, request.CustomerId,
                        tenant.OrganizationId),
                    cancellationToken);

                return result.Match(id => Results.Created($"/api/jobs/{id}", new { id }));
            })
            .WithTags("Jobs");
}
