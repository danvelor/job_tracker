using JobTracker.Common.Presentation;

namespace JobTracker.Api.Authentication;

/// <summary>
/// Registered only when the environment is Development (architecture 7.1),
/// which is why it is not an <see cref="IEndpoint"/>: the assembly scan would
/// map it everywhere, and an endpoint that mints a token for any organization
/// is a tenant isolation bypass with a friendly name.
/// </summary>
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
