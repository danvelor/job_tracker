using System.Net;
using System.Net.Sockets;
using Hangfire.Dashboard;

namespace JobTracker.Api.Authentication;

internal sealed class LocalOnlyDashboardFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) =>
        ShouldAllow(context.GetHttpContext().Connection.RemoteIpAddress);

    internal bool ShouldAllow(IPAddress? candidate)
    {
        if (candidate is null)
        {
            return false;
        }

        var remote = candidate.IsIPv4MappedToIPv6 ? candidate.MapToIPv4() : candidate;

        if (IPAddress.IsLoopback(remote))
        {
            return true;
        }

        return remote.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPrivateV4(remote),
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
            172 => octets[1] >= 16 && octets[1] <= 31,
            192 => octets[1] == 168,
            169 => octets[1] == 254,
            _ => false,
        };
    }
}
