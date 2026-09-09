using System.Net;
using Hangfire.Dashboard;

namespace JobTracker.Api.Authentication;

/// <summary>
/// The Hangfire dashboard cannot be protected the way everything else is. A
/// browser navigating to <c>/hangfire</c> sends no <c>Authorization</c> header,
/// so the fallback policy would refuse every request and the feature would be
/// dead rather than protected — and architecture 7.5 wants it alive, because it
/// makes the whole async pipeline inspectable without reading logs.
///
/// So it gets two conditions instead, both cheap, and neither depending on the
/// other being right. The host registers it only in Development; this filter
/// refuses any request that did not come from loopback.
///
/// Loopback is exactly the reviewer's case — a published port from the host, or
/// <c>docker compose exec</c> — and nobody else's. Basic auth was the
/// alternative, and it adds a credential to invent, document and eventually
/// leak.
/// </summary>
internal sealed class LocalOnlyDashboardFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) =>
        ShouldAllow(context.GetHttpContext().Connection.RemoteIpAddress);

    /// <summary>
    /// Internal so it can be tested without a DashboardContext, which cannot be
    /// constructed outside Hangfire.
    /// </summary>
    internal bool ShouldAllow(IPAddress? remote) =>
        // A request whose address the server could not determine is not
        // evidence of being local. Treating unknown as permission is how an
        // allowlist becomes decoration.
        remote is not null && IPAddress.IsLoopback(remote);
}
