using JobTracker.Common.Infrastructure;
using JobTracker.Common.Presentation;
using JobTracker.Modules.Jobs.Application.Parties;
using MediatR;

namespace JobTracker.Modules.Jobs.Presentation.Parties;

internal sealed class ListCustomers : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/api/customers", async (
                ITenantContext tenant, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListCustomersQuery(tenant.OrganizationId), cancellationToken);

                return result.Match(Results.Ok);
            })
            .WithTags("Rosters");
}
