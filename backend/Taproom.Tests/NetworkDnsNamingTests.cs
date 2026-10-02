using Taproom.Api.Clients;

namespace Taproom.Tests;

public class NetworkDnsNamingTests
{
    private static DnsQueryEntry Query(string domain, int secondsAgo) => new()
    {
        Time = DateTimeOffset.UtcNow.AddSeconds(-secondsAgo),
        Domain = domain,
        Type = "A",
        Blocked = false,
        Failed = false,
        Rcode = "NOERROR",
    };

    private static FirewallState State(string srcAddr, string remoteIp) => new()
    {
        SrcAddr = srcAddr,
        Protocol = "tcp",
        RemoteIp = remoteIp,
        RemotePort = 443,
        State = "ESTABLISHED",
        DownBytes = 1000,
        UpBytes = 100,
        Packets = 10,
        AgeSeconds = 5,
    };

    [Fact]
    public void Extracts_distinct_domains_most_recent_first()
    {
        var entries = new[] { Query("b.example.com", 1), Query("a.example.com", 5), Query("b.example.com", 10) };

        var domains = NetworkDnsNaming.ExtractDomainsMostRecentFirst(entries);

        Assert.Equal(["b.example.com", "a.example.com"], domains);
    }

    [Fact]
    public void One_devices_lookup_names_another_devices_connection()
    {
        // Unbound's cache holds streaming.example.com → 5.5.5.5 (device A looked it up). Device B has its
        // own connection to 5.5.5.5 and never queried it, yet gets the name from the shared cache map.
        var cacheMap = DnsCacheMap.Build(
        [
            new DnsCacheRecord { Host = "streaming.example.com.", RrType = "A", Value = "5.5.5.5", TtlSeconds = 120 },
        ]);
        var networkWideQueries = new[] { Query("streaming.example.com", 30) };
        var dnsNamesByIp = DnsNaming.ChooseNames(
            cacheMap, ["5.5.5.5"], NetworkDnsNaming.ExtractDomainsMostRecentFirst(networkWideQueries));

        var states = new[] { State("10.0.1.20", "5.5.5.5") };
        var geo = new Dictionary<string, GeoInfo> { ["5.5.5.5"] = new() { Country = "US", City = "Ashburn", Lat = 1, Lon = 1 } };

        var outcome = ConnectionEnricher.Enrich(
            states, dnsNamesByIp, new Dictionary<string, string?>(), geo, new Dictionary<string, string>(), dnsQueryCountInWindow: 1);

        var connection = Assert.Single(outcome.Connections);
        Assert.Equal("streaming.example.com", connection.Name);
        Assert.Equal(NameSource.Dns, connection.NameSource);
    }
}
