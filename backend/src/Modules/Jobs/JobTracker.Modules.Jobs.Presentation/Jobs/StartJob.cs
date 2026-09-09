using JobTracker.Common.Infrastructure;
using JobTracker.Common.Presentation;
using JobTracker.Modules.Jobs.Application.Jobs.StartJob;
using MediatR;

namespace JobTracker.Modules.Jobs.Presentation.Jobs;

internal sealed class StartJob : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/api/jobs/{id:guid}/start", async (
                Guid id,
                ITenantContext tenant,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new StartJobCommand(id, tenant.OrganizationId), cancellationToken);

                return result.Match(Results.NoContent);
            })
            .WithTags("Jobs");
}
