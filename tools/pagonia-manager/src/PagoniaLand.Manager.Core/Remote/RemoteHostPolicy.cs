using System.Net;
using System.Net.Sockets;

namespace PagoniaLand.Manager;

/// <summary>
/// Shared SSRF host policy for every outbound fetch. Refuses loopback, the unspecified address
/// (<c>0.0.0.0</c> / <c>::</c>, which the OS routes to localhost), link-local, and the
/// cloud-metadata endpoint (<c>169.254.169.254</c>) — the targets an attacker-supplied or
/// redirect-reached URL would aim at to reach internal services. Private LAN ranges (IPv4
/// <c>10/8</c>, <c>192.168/16</c>, … and the IPv6 unique-local equivalent) stay allowed, so a
/// legitimate internal mirror still works.
/// <para>
/// <see cref="IsBlocked"/> is the cheap parse-time / per-hop check on a literal-IP or loopback
/// host. The authoritative defence is at connection time: the HTTP handler's
/// <c>ConnectCallback</c> resolves the host's A/AAAA records and runs <see cref="IsBlockedAddress"/>
/// on each, connecting only to allowed addresses — so a DNS name that resolves to an internal IP
/// (DNS-rebinding) is refused too, and an IPv4-mapped IPv6 spelling of a blocked address can't slip
/// past. That re-resolution at connect time also closes the parse-time TOCTOU window.
/// </para>
/// </summary>
public static class RemoteHostPolicy
{
    public static bool IsBlocked(Uri uri)
        => uri.IsLoopback || (IPAddress.TryParse(uri.Host, out var ip) && IsBlockedAddress(ip));

    /// <summary>True when <paramref name="ip"/> is a loopback / link-local / metadata address an
    /// outbound fetch must not reach. Public so the connection-time guard can validate each resolved
    /// address before connecting.</summary>
    public static bool IsBlockedAddress(IPAddress ip)
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

        // The unspecified address (0.0.0.0 / ::) is not "loopback", but a TCP connect() to it is
        // routed by the OS to a service listening on localhost — so an attacker-supplied or
        // redirect-reached http://0.0.0.0:<port>/ would reach an internal listener. Block it like
        // loopback (covers the IPv4-mapped ::ffff:0.0.0.0 too, already unwrapped above).
        if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
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
