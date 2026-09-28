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
}
