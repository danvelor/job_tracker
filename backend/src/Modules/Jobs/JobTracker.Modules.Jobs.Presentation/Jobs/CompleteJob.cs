using JobTracker.Common.Infrastructure;
using JobTracker.Common.Presentation;
using JobTracker.Modules.Jobs.Application.Jobs.CompleteJob;
using MediatR;

namespace JobTracker.Modules.Jobs.Presentation.Jobs;

internal sealed class CompleteJob : IEndpoint
{
    internal sealed record Photo(string Url, string? Caption);

    internal sealed record Request(string SignatureUrl, IReadOnlyList<Photo>? Photos);

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/api/jobs/{id:guid}/complete", async (
                Guid id,
                Request request,
                ITenantContext tenant,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CompleteJobCommand(
                        id,
                        tenant.OrganizationId,
                        request.SignatureUrl,
                        request.Photos?
                            .Select(photo => new NewPhotoInput(photo.Url, photo.Caption))
                            .ToList() ?? []),
                    cancellationToken);

                return result.Match(Results.NoContent);
            })
            .WithTags("Jobs");
}
