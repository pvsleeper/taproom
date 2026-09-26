namespace Taproom.Api.Clients;

/// <summary>
/// Pure function that merges Omada clients, OPNsense ARP entries and OPNsense DHCP leases into one
/// client list, keyed by MAC. No I/O, so it is fully unit-testable against captured JSON fixtures.
/// </summary>
public static class ClientMerger
{
    public static IReadOnlyList<NetworkClient> Merge(
        IReadOnlyList<OmadaClientRecord> omadaClients,
        IReadOnlyList<ArpEntry> arpEntries,
        IReadOnlyList<DhcpLease> dhcpLeases)
    {
        var byMac = new Dictionary<string, MergeBucket>();

        foreach (var c in omadaClients)
        {
            var mac = MacAddress.Normalize(c.Mac);
            if (mac is null) continue;
            GetOrAdd(byMac, mac).Omada = c;
        }

        foreach (var a in arpEntries)
        {
            var mac = MacAddress.Normalize(a.Mac);
            if (mac is null) continue;
            GetOrAdd(byMac, mac).Arp = a;
        }

        foreach (var d in dhcpLeases)
        {
            var mac = MacAddress.Normalize(d.Mac);
            if (mac is null) continue;
            GetOrAdd(byMac, mac).Dhcp = d;
        }

        var result = new List<NetworkClient>(byMac.Count);
        foreach (var (mac, bucket) in byMac)
        {
            result.Add(BuildClient(mac, bucket));
        }

        return result;
    }

    private static MergeBucket GetOrAdd(Dictionary<string, MergeBucket> map, string mac)
    {
        if (!map.TryGetValue(mac, out var bucket))
        {
            bucket = new MergeBucket();
            map[mac] = bucket;
        }
        return bucket;
    }

    private static NetworkClient BuildClient(string mac, MergeBucket bucket)
    {
        var omada = bucket.Omada;
        var arp = bucket.Arp;
        var dhcp = bucket.Dhcp;

        var name = FirstNonEmpty(omada?.CustomName, dhcp?.Hostname, omada?.Hostname, arp?.Hostname) ?? mac;
        var hostname = FirstNonEmpty(dhcp?.Hostname, omada?.Hostname, arp?.Hostname);
        var ip = FirstNonEmpty(arp?.Ip, dhcp?.Ip, omada?.Ip);

        var connection = omada?.Wireless == true ? ConnectionType.Wireless : ConnectionType.Wired;
        var online = omada?.Active == true || arp is not null;

        var sources = new List<string>(3);
        if (omada is not null) sources.Add("omada");
        if (arp is not null) sources.Add("arp");
        if (dhcp is not null) sources.Add("dhcp");

        var lastSeen = Max(omada?.LastSeen, arp?.ObservedAt, dhcp?.ObservedAt) ?? DateTimeOffset.UtcNow;

        return new NetworkClient
        {
            Mac = mac,
            Name = name,
            Hostname = hostname,
            Ip = ip,
            Connection = connection,
            Online = online,
            Ssid = omada?.Ssid,
            ApName = omada?.ApName,
            Band = omada?.Band,
            Rssi = omada?.Rssi,
            RxBytes = omada?.RxBytes,
            TxBytes = omada?.TxBytes,
            ConnectedSince = omada?.ConnectedSince,
            LastSeen = lastSeen,
            IsStaticLease = dhcp?.IsStatic,
            Sources = sources.ToArray(),
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v;
            }
        }
        return null;
    }

    private static DateTimeOffset? Max(params DateTimeOffset?[] values)
    {
        DateTimeOffset? max = null;
        foreach (var v in values)
        {
            if (v is null) continue;
            if (max is null || v > max) max = v;
        }
        return max;
    }

    private sealed class MergeBucket
    {
        public OmadaClientRecord? Omada;
        public ArpEntry? Arp;
        public DhcpLease? Dhcp;
    }
}
