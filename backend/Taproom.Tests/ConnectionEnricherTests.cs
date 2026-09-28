using Taproom.Api.Clients;

namespace Taproom.Tests;

public class ConnectionEnricherTests
{
    private static FirewallState State(string ip, int port, long bytes = 1000, long packets = 10, int age = 60, string proto = "tcp", string state = "ESTABLISHED") =>
        new()
        {
            Protocol = proto,
            RemoteIp = ip,
            RemotePort = port,
            State = state,
            Bytes = bytes,
            Packets = packets,
            AgeSeconds = age,
        };

    private static readonly IReadOnlyDictionary<string, GeoInfo> SydneyGeo = new Dictionary<string, GeoInfo>
    {
        ["1.1.1.1"] = new GeoInfo { Country = "AU", City = "Sydney", Lat = -33.87, Lon = 151.21, Asn = 100, Org = "Example Org" },
        ["1.1.1.2"] = new GeoInfo { Country = "AU", City = "Sydney", Lat = -33.86, Lon = 151.20, Asn = 100, Org = "Example Org" },
        ["2.2.2.2"] = new GeoInfo { Country = "US", City = "Ashburn", Lat = 39.04, Lon = -77.48, Asn = 200, Org = "Other Org" },
    };

    private static EnrichmentOutcome Enrich(
        IReadOnlyList<FirewallState> states,
        IReadOnlyDictionary<string, List<string>>? dnsNames = null,
        IReadOnlyDictionary<string, string?>? ptrNames = null,
        IReadOnlyDictionary<string, GeoInfo>? geo = null,
        IReadOnlyDictionary<string, string>? localNames = null,
        int dnsQueryCount = 5) =>
        ConnectionEnricher.Enrich(
            states,
            dnsNames ?? new Dictionary<string, List<string>>(),
            ptrNames ?? new Dictionary<string, string?>(),
            geo ?? new Dictionary<string, GeoInfo>(),
            localNames ?? new Dictionary<string, string>(),
            dnsQueryCount);

    [Fact]
    public void Names_connection_from_dns_when_ip_matches_a_recently_queried_domain()
    {
        var outcome = Enrich(
            [State("1.1.1.1", 443)],
            dnsNames: new Dictionary<string, List<string>> { ["1.1.1.1"] = ["youtube.com"] },
            geo: SydneyGeo);

        var connection = Assert.Single(outcome.Connections);
        Assert.Equal("youtube.com", connection.Name);
        Assert.Equal(NameSource.Dns, connection.NameSource);
    }

    [Fact]
    public void Most_recently_queried_domain_is_primary_name_and_others_go_to_other_names()
    {
        var outcome = Enrich(
            [State("1.1.1.1", 443)],
            dnsNames: new Dictionary<string, List<string>> { ["1.1.1.1"] = ["youtube.com", "i.ytimg.com"] },
            geo: SydneyGeo);

        var connection = Assert.Single(outcome.Connections);
        Assert.Equal("youtube.com", connection.Name);
        Assert.Equal(["i.ytimg.com"], connection.OtherNames);
    }

    [Fact]
    public void Falls_back_to_ptr_when_no_dns_match()
    {
        var outcome = Enrich(
            [State("1.1.1.1", 443)],
            ptrNames: new Dictionary<string, string?> { ["1.1.1.1"] = "edge.example.net" },
            geo: SydneyGeo);

        var connection = Assert.Single(outcome.Connections);
        Assert.Equal("edge.example.net", connection.Name);
        Assert.Equal(NameSource.Ptr, connection.NameSource);
    }

    [Fact]
    public void Falls_back_to_asn_organisation_when_no_dns_or_ptr_match()
    {
        var outcome = Enrich([State("1.1.1.1", 443)], geo: SydneyGeo);

        var connection = Assert.Single(outcome.Connections);
        Assert.Equal("Example Org", connection.Name);
        Assert.Equal(NameSource.Asn, connection.NameSource);
    }

    [Fact]
    public void Falls_back_to_bare_ip_when_nothing_resolves()
    {
        var geoWithNoOrg = new Dictionary<string, GeoInfo>
        {
            ["1.1.1.1"] = new GeoInfo { Country = "AU", City = "Sydney", Lat = -33.87, Lon = 151.21 },
        };
        var outcome = Enrich([State("1.1.1.1", 443)], geo: geoWithNoOrg);

        var connection = Assert.Single(outcome.Connections);
        Assert.Equal("1.1.1.1", connection.Name);
        Assert.Equal(NameSource.Ip, connection.NameSource);
    }

    [Fact]
    public void Private_destination_goes_to_local_not_connections_or_markers()
    {
        var outcome = Enrich(
            [State("10.0.1.10", 32400)],
            localNames: new Dictionary<string, string> { ["10.0.1.10"] = "brewhouse" });

        Assert.Empty(outcome.Connections);
        Assert.Empty(outcome.Markers);
        var local = Assert.Single(outcome.Local);
        Assert.Equal("brewhouse", local.Name);
        Assert.Equal("10.0.1.10", local.RemoteIp);
    }

    [Fact]
    public void Public_ip_with_no_geo_match_goes_to_unknown_location_not_connections_or_markers()
    {
        var outcome = Enrich([State("9.9.9.9", 443)]);

        Assert.Empty(outcome.Connections);
        Assert.Empty(outcome.Markers);
        Assert.Single(outcome.UnknownLocation);
    }

    [Fact]
    public void States_to_same_remote_ip_and_port_are_grouped_with_summed_bytes_and_packets()
    {
        var outcome = Enrich(
            [State("1.1.1.1", 443, bytes: 100, packets: 5, age: 30), State("1.1.1.1", 443, bytes: 200, packets: 8, age: 90)],
            geo: SydneyGeo);

        var connection = Assert.Single(outcome.Connections);
        Assert.Equal(300, connection.Bytes);
        Assert.Equal(13, connection.Packets);
        Assert.Equal(90, connection.AgeSeconds);
    }

    [Fact]
    public void Different_remote_ports_on_same_ip_are_not_grouped_together()
    {
        var outcome = Enrich([State("1.1.1.1", 443), State("1.1.1.1", 8443)], geo: SydneyGeo);

        Assert.Equal(2, outcome.Connections.Count);
    }

    [Fact]
    public void Connections_at_the_same_rounded_location_are_grouped_into_one_marker()
    {
        var outcome = Enrich(
            [State("1.1.1.1", 443, bytes: 100), State("1.1.1.2", 443, bytes: 200), State("2.2.2.2", 443, bytes: 50)],
            geo: SydneyGeo);

        Assert.Equal(3, outcome.Connections.Count);
        Assert.Equal(2, outcome.Markers.Count);

        var sydney = outcome.Markers.Single(m => m.Label.StartsWith("Sydney"));
        Assert.Equal(2, sydney.ConnectionCount);
        Assert.Equal(300, sydney.Bytes);
    }

    [Fact]
    public void Hints_possible_own_dns_when_several_public_connections_have_no_dns_names_and_no_queries_seen()
    {
        var outcome = Enrich(
            [State("1.1.1.1", 443), State("1.1.1.2", 443), State("2.2.2.2", 443)],
            geo: SydneyGeo,
            dnsQueryCount: 0);

        Assert.Contains("possibleOwnDns", outcome.Hints);
    }

    [Fact]
    public void No_hint_when_dns_queries_were_seen_for_the_client()
    {
        var outcome = Enrich(
            [State("1.1.1.1", 443), State("1.1.1.2", 443), State("2.2.2.2", 443)],
            geo: SydneyGeo,
            dnsQueryCount: 10);

        Assert.DoesNotContain("possibleOwnDns", outcome.Hints);
    }

    [Fact]
    public void No_hint_when_at_least_one_connection_was_named_via_dns()
    {
        var outcome = Enrich(
            [State("1.1.1.1", 443), State("1.1.1.2", 443), State("2.2.2.2", 443)],
            dnsNames: new Dictionary<string, List<string>> { ["1.1.1.1"] = ["example.com"] },
            geo: SydneyGeo,
            dnsQueryCount: 0);

        Assert.DoesNotContain("possibleOwnDns", outcome.Hints);
    }
}
