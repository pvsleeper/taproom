using System.Net;

namespace Taproom.Api.Clients;

public static class PrivateIp
{
    /// <summary>True for RFC1918/loopback/link-local addresses — anything that isn't a real public destination.</summary>
    public static bool IsPrivateOrLinkLocal(string ip)
    {
        if (!IPAddress.TryParse(ip, out var address))
        {
            return false;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] is >= 16 and <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254)
                || b[0] == 127;
        }

        return IPAddress.IsLoopback(address)
            || address.IsIPv6LinkLocal
            || address.IsIPv6UniqueLocal
            || address.IsIPv6SiteLocal;
    }

    /// <summary>
    /// True only for genuinely public unicast addresses — the only ones worth a reverse lookup. Excludes
    /// 0.0.0.0/8, private, CGNAT (100.64.0.0/10), loopback, link-local, IETF/documentation/benchmark
    /// ranges, multicast (224.0.0.0/4, ff00::/8) and reserved/broadcast (240.0.0.0/4 incl. 255.255.255.255);
    /// for IPv6 only global unicast (2000::/3, minus 2001:db8::/32) qualifies. IPv4-mapped IPv6 is judged as IPv4.
    /// </summary>
    public static bool IsPubliclyRoutableUnicast(string ip)
    {
        if (!IPAddress.TryParse(ip, out var address))
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0
                || b[0] == 10
                || (b[0] == 100 && b[1] is >= 64 and <= 127)
                || b[0] == 127
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 172 && b[1] is >= 16 and <= 31)
                || (b[0] == 192 && b[1] == 0 && b[2] is 0 or 2)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 198 && b[1] is 18 or 19)
                || (b[0] == 198 && b[1] == 51 && b[2] == 100)
                || (b[0] == 203 && b[1] == 0 && b[2] == 113)
                || b[0] >= 224);
        }

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return false;
        }

        var v6 = address.GetAddressBytes();
        var isGlobalUnicast = (v6[0] & 0xE0) == 0x20;
        var isDocumentation = v6[0] == 0x20 && v6[1] == 0x01 && v6[2] == 0x0d && v6[3] == 0xb8;
        return isGlobalUnicast && !isDocumentation;
    }
}
