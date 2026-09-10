using System.Security.Claims;
using JobTracker.Api.Authentication;
using JobTracker.Common.Infrastructure;

namespace JobTracker.Api;

internal sealed class HttpTenantContext(IHttpContextAccessor accessor)
    : ITenantContext, ITenantContextSetter
{
    private Guid? _override;

    public IDisposable Use(Guid organizationId)
    {
        var previous = _override;
        _override = organizationId;

        return new Restore(() => _override = previous);
    }

    public Guid OrganizationId
    {
        get
        {
            if (_override is { } declared)
            {
                return declared;
            }

            var claim = accessor.HttpContext?.User.FindFirstValue(TokenIssuer.OrganizationClaim);

            return Guid.TryParse(claim, out var organizationId)
                ? organizationId
                : throw new InvalidOperationException(
                    "No organization claim on a validated principal.");
        }
    }

    private sealed class Restore(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}
