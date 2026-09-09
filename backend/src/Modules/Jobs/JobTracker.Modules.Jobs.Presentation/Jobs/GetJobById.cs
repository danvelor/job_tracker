using JobTracker.Common.Infrastructure;
using JobTracker.Common.Presentation;
using JobTracker.Modules.Jobs.Application.Jobs.GetJobById;
using MediatR;

namespace JobTracker.Modules.Jobs.Presentation.Jobs;

internal sealed class GetJobById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/api/jobs/{id:guid}", async (
                Guid id,
                ITenantContext tenant,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetJobByIdQuery(id, tenant.OrganizationId), cancellationToken);

                return result.Match(Results.Ok);
            })
            .WithTags("Jobs");
}
