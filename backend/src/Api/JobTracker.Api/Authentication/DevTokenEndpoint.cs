using JobTracker.Common.Presentation;

namespace JobTracker.Api.Authentication;

internal static class DevTokenEndpoint
{
    internal sealed record Request(Guid OrganizationId);

    public static void MapDevToken(this WebApplication app) =>
        app.MapPost("/auth/dev-token", (Request request, TokenIssuer issuer) =>
                Results.Ok(new
                {
                    token = issuer.Issue(request.OrganizationId, "dev-user", "Development user"),
                }))
            .AllowAnonymous()
            .WithTags("Authentication");
}
