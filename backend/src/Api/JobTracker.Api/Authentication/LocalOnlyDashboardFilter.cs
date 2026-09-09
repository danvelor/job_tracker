using System.Net;
using System.Net.Sockets;
using Hangfire.Dashboard;

namespace JobTracker.Api.Authentication;

/// <summary>
/// The Hangfire dashboard cannot be protected the way everything else is. A
/// browser navigating to <c>/hangfire</c> sends no <c>Authorization</c> header,
/// so the fallback policy would refuse every request and the feature would be
/// dead rather than guarded — and architecture 7.5 wants it alive, because it
/// makes the whole async pipeline inspectable without reading logs.
///
/// <b>The real guard is that this is registered only in Development.</b> This
/// filter is a second, weaker condition, and it is worth being precise about
/// what it buys: it refuses a public client. It does not refuse a request
/// forwarded by a load balancer, which arrives from a private address, so it is
/// defence in depth against <c>ASPNETCORE_ENVIRONMENT</c> being wrong rather
/// than a boundary anything should rely on.
///
/// It accepts the private ranges, not only loopback, and that was a correction
/// rather than a choice. A published Docker port does not arrive from
/// 127.0.0.1: the container sees the bridge gateway, 172.18.0.1 here. Loopback
/// alone left the dashboard unreachable in the one environment it exists for,
/// and the README promising the URL is what caught it.
///
/// Basic auth was the alternative. It adds a credential to invent, document and
/// eventually leak, for a page that exists so a reviewer can watch a queue
/// drain.
/// </summary>
internal sealed class LocalOnlyDashboardFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) =>
        ShouldAllow(context.GetHttpContext().Connection.RemoteIpAddress);

    /// <summary>
    /// Internal so it can be tested without a <see cref="DashboardContext"/>,
    /// which cannot be constructed outside Hangfire.
    /// </summary>
    internal bool ShouldAllow(IPAddress? candidate)
    {
        // A request whose address the server could not determine is not
        // evidence of being local. Treating unknown as permission is how an
        // allowlist becomes decoration.
        if (candidate is null)
        {
            return false;
        }

        // Kestrel binds dual-stack, so an IPv4 client arrives as
        // ::ffff:192.168.65.1 rather than as 192.168.65.1. IsLoopback unmaps
        // internally and the range check below does not, which is exactly why
        // the dashboard worked from inside the container and answered 401 from
        // the host.
        var remote = candidate.IsIPv4MappedToIPv6 ? candidate.MapToIPv4() : candidate;

        if (IPAddress.IsLoopback(remote))
        {
            return true;
        }

        return remote.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPrivateV4(remote),
            // fc00::/7 unique-local and fe80::/10 link-local. The same two
            // categories as the v4 ranges below.
            AddressFamily.InterNetworkV6 => remote.IsIPv6LinkLocal || remote.IsIPv6UniqueLocal,
            _ => false,
        };
    }

    private static bool IsPrivateV4(IPAddress remote)
    {
        var octets = remote.GetAddressBytes();

        return octets[0] switch
        {
            10 => true,
            // 172.16/12 ends at 172.31.255.255. Written as a range rather than
            // a mask because the boundary is the part a reader checks, and it
            // is the part a hand-written mask usually gets wrong.
            172 => octets[1] >= 16 && octets[1] <= 31,
            192 => octets[1] == 168,
            // 169.254/16, link-local. Present for the same reason as the
            // others: it is an address a machine reaches itself by.
            169 => octets[1] == 254,
            _ => false,
        };
    }
}
