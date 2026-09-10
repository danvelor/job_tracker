using Microsoft.AspNetCore.Routing;

namespace JobTracker.Common.Presentation;

public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
