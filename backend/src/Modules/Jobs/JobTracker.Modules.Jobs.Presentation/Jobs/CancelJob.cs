using JobTracker.Common.Infrastructure;
using JobTracker.Common.Presentation;
using JobTracker.Modules.Jobs.Application.Jobs.CancelJob;
using MediatR;

namespace JobTracker.Modules.Jobs.Presentation.Jobs;

internal sealed class CancelJob : IEndpoint
{
    internal sealed record Request(string Reason);

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/api/jobs/{id:guid}/cancel", async (
                Guid id,
                Request request,
                ITenantContext tenant,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CancelJobCommand(id, tenant.OrganizationId, request.Reason),
                    cancellationToken);

                return result.Match(Results.NoContent);
            })
            .WithTags("Jobs");
}
