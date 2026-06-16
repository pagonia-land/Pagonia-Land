using System.Net;
using System.Net.Sockets;

namespace PagoniaLand.Manager;

/// <summary>
/// Shared SSRF host policy for every outbound fetch. Refuses loopback, link-local, and the
/// cloud-metadata endpoint (<c>169.254.169.254</c>) — the targets an attacker-supplied or
/// redirect-reached URL would aim at to reach internal services. Private LAN ranges (IPv4
/// <c>10/8</c>, <c>192.168/16</c>, … and the IPv6 unique-local equivalent) stay allowed, so a
/// legitimate internal mirror still works. Applied both at parse time and on every HTTP hop
/// (including redirects) so the guard can't be bypassed by a 3xx to an internal host, nor by an
/// IPv4-mapped IPv6 spelling of a blocked address.
/// </summary>
public static class RemoteHostPolicy
{
    public static bool IsBlocked(Uri uri)
        => uri.IsLoopback || (IPAddress.TryParse(uri.Host, out var ip) && IsBlockedAddress(ip));

    private static bool IsBlockedAddress(IPAddress ip)
    {
        // Unwrap an IPv4-mapped IPv6 address (e.g. ::ffff:127.0.0.1, ::ffff:169.254.169.254) so the
        // IPv4 rules below see the real address rather than an IPv6 spelling that slips past them.
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 169 && bytes[1] == 254; // 169.254/16 link-local (incl. the metadata IP)
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal; // fe80::/10 (ULA fc00::/7 stays allowed, mirroring IPv4 private LAN)
        }
        return false;
    }
}
