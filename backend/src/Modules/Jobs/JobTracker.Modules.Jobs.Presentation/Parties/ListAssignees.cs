using JobTracker.Common.Infrastructure;
using JobTracker.Common.Presentation;
using JobTracker.Modules.Jobs.Application.Parties;
using MediatR;

namespace JobTracker.Modules.Jobs.Presentation.Parties;

/// <summary>Read-only (D-26). There is no POST, and that is the design.</summary>
internal sealed class ListAssignees : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/api/assignees", async (
                ITenantContext tenant, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListAssigneesQuery(tenant.OrganizationId), cancellationToken);

                return result.Match(Results.Ok);
            })
            .WithTags("Rosters");
}
