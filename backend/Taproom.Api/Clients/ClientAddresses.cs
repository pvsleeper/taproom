using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Clients;

/// <summary>Finds every address (IPv4 + IPv6) a client is currently known by, per the phase 4 rule of matching on the full address set.</summary>
public static class ClientAddresses
{
    public static async Task<IReadOnlyList<string>> GetAsync(
        IOpnsenseClient opnsense, string mac, string ipv4, CancellationToken cancellationToken)
    {
        var ndp = await opnsense.GetNdpTableAsync(cancellationToken);
        var ipv6Addresses = ndp.Where(n => n.Mac == mac).Select(n => n.Ipv6);

        var addresses = new List<string> { ipv4 };
        addresses.AddRange(ipv6Addresses);
        return addresses;
    }
}
