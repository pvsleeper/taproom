using System.Text.Json;
using Taproom.Api.Clients;
using Taproom.Api.Sources.Opnsense;

namespace Taproom.Tests;

public class DnsCacheMapTests
{
    private static DnsCacheMap BuildFromFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "unbound-dumpcache.json");
        var dump = JsonSerializer.Deserialize<OpnsenseUnboundCacheDump>(File.ReadAllText(path))!;
        return DnsCacheMap.Build(UnboundCacheClient.Map(dump));
    }

    [Fact]
    public void Dump_mapping_keeps_only_address_and_cname_records_with_parsed_ttl()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "unbound-dumpcache.json");
        var dump = JsonSerializer.Deserialize<OpnsenseUnboundCacheDump>(File.ReadAllText(path))!;

        var records = UnboundCacheClient.Map(dump);

        Assert.DoesNotContain(records, r => r.RrType is "NS" or "RRSIG");
        Assert.Equal(60, records.Single(r => r.Value == "192.0.2.5").TtlSeconds);
    }

    [Fact]
    public void Cname_aliases_are_followed_back_to_the_names_a_device_asked_for()
    {
        var map = BuildFromFixture();

        var candidates = map.CandidatesByIp["203.0.113.10"];

        // Both aliases of the CDN name come first; the CDN's own name is only a fallback after them.
        Assert.Equal(["shop.example.org", "www.example.com", "edge.example-cdn.net"], candidates);
    }

    [Fact]
    public void Multi_hop_chains_resolve_to_the_head_of_the_chain()
    {
        var map = BuildFromFixture();

        var candidates = map.CandidatesByIp["192.0.2.5"];

        Assert.Equal("a.example.com", candidates[0]);
        Assert.Contains("b.example.com", candidates);
        Assert.Contains("c.example-cdn.net", candidates);
    }

    [Fact]
    public void Ipv4_and_ipv6_records_both_map_and_ipv6_uses_the_compressed_form()
    {
        var map = BuildFromFixture();

        Assert.Contains("www.example.com", map.CandidatesByIp["2001:db8::10"]);
        Assert.Contains("www.example.com", map.CandidatesByIp["203.0.113.10"]);
    }

    [Fact]
    public void A_name_with_no_aliases_is_its_own_head()
    {
        var map = BuildFromFixture();

        Assert.Equal(["direct.example.net"], map.CandidatesByIp["198.51.100.7"]);
    }

    [Fact]
    public void Expired_records_are_ignored()
    {
        Assert.False(BuildFromFixture().CandidatesByIp.ContainsKey("192.0.2.99"));
    }

    [Fact]
    public void Names_are_lowercased_without_a_trailing_dot()
    {
        Assert.Equal(["mixed.example.com"], BuildFromFixture().CandidatesByIp["192.0.2.77"]);
    }

    [Fact]
    public void A_cname_cycle_terminates_and_still_yields_candidates()
    {
        var candidates = BuildFromFixture().CandidatesByIp["192.0.2.88"];

        Assert.Contains("loop1.example.com", candidates);
        Assert.Contains("loop2.example.com", candidates);
    }
}
