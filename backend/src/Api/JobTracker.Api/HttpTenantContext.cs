using System.Security.Claims;
using JobTracker.Api.Authentication;
using JobTracker.Common.Infrastructure;

namespace JobTracker.Api;

/// <summary>
/// Layer 1 of architecture 7.2. The organization comes from the validated
/// <c>org</c> claim and from nowhere else — no endpoint accepts one, because
/// accepting one would invite forging it.
/// </summary>
internal sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid OrganizationId
    {
        get
        {
            var claim = accessor.HttpContext?.User.FindFirstValue(TokenIssuer.OrganizationClaim);

            // Reaching a tenant-scoped query without a tenant is a defect in
            // the pipeline rather than a request the caller got wrong: every
            // route requires authorization, so a missing claim means a
            // validated token lacked one. Returning Guid.Empty would answer
            // with an empty list and hide it.
            return Guid.TryParse(claim, out var organizationId)
                ? organizationId
                : throw new InvalidOperationException(
                    "No organization claim on a validated principal.");
        }
    }
}
