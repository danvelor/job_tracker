using JobTracker.Common.Infrastructure;
using JobTracker.Common.Presentation;
using JobTracker.Modules.Jobs.Application.Jobs.RescheduleJob;
using MediatR;

namespace JobTracker.Modules.Jobs.Presentation.Jobs;

/// <summary>
/// FR-2, correcting a job that is still Scheduled. PATCH rather than POST
/// because it changes two fields of an existing resource rather than driving a
/// transition (design B6).
/// </summary>
internal sealed class RescheduleJob : IEndpoint
{
    internal sealed record Request(DateOnly ScheduledDate, Guid AssigneeId);

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPatch("/api/jobs/{id:guid}/schedule", async (
                Guid id,
                Request request,
                ITenantContext tenant,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RescheduleJobCommand(
                        id, tenant.OrganizationId, request.ScheduledDate, request.AssigneeId),
                    cancellationToken);

                return result.Match(Results.NoContent);
            })
            .WithTags("Jobs");
}
