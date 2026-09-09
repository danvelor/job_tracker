using Microsoft.AspNetCore.Routing;

namespace JobTracker.Common.Presentation;

/// <summary>
/// One endpoint per type, discovered by an assembly scan. A module exposes its
/// routes without the composition root naming any of them, so adding an
/// endpoint is a change in one project rather than two.
/// </summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
