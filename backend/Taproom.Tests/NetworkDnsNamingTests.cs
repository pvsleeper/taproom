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
        // Device A (10.0.1.10) looked up streaming.example.com, which resolves to 5.5.5.5.
        var networkWideQueries = new[] { Query("streaming.example.com", 30) };
        var domainsMostRecentFirst = NetworkDnsNaming.ExtractDomainsMostRecentFirst(networkWideQueries);
        var dnsNamesByIp = new Dictionary<string, List<string>>
        {
            ["5.5.5.5"] = domainsMostRecentFirst.Where(d => d == "streaming.example.com").ToList(),
        };

        // Device B (10.0.1.20) has its own raw connection to the same IP, with no lookup of its own.
        var states = new[] { State("10.0.1.20", "5.5.5.5") };
        var geo = new Dictionary<string, GeoInfo> { ["5.5.5.5"] = new() { Country = "US", City = "Ashburn", Lat = 1, Lon = 1 } };

        var outcome = ConnectionEnricher.Enrich(
            states, dnsNamesByIp, new Dictionary<string, string?>(), geo, new Dictionary<string, string>(), dnsQueryCountInWindow: 1);

        var connection = Assert.Single(outcome.Connections);
        Assert.Equal("streaming.example.com", connection.Name);
        Assert.Equal(NameSource.Dns, connection.NameSource);
    }
}
