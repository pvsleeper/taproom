using Taproom.Api.Clients;

namespace Taproom.Tests;

public class DnsNamingTests
{
    private static DnsCacheMap MapWithTwoAliases() => DnsCacheMap.Build(
    [
        new DnsCacheRecord { Host = "www.example.com.", RrType = "CNAME", Value = "edge.cdn.net.", TtlSeconds = 300 },
        new DnsCacheRecord { Host = "shop.example.org.", RrType = "CNAME", Value = "edge.cdn.net.", TtlSeconds = 300 },
        new DnsCacheRecord { Host = "edge.cdn.net.", RrType = "A", Value = "203.0.113.10", TtlSeconds = 100 },
    ]);

    [Fact]
    public void Prefers_a_domain_the_client_recently_queried()
    {
        var names = DnsNaming.ChooseNames(MapWithTwoAliases(), ["203.0.113.10"], ["www.example.com"]);

        Assert.Equal("www.example.com", names["203.0.113.10"][0]);
    }

    [Fact]
    public void Among_queried_candidates_the_most_recent_wins()
    {
        var names = DnsNaming.ChooseNames(MapWithTwoAliases(), ["203.0.113.10"], ["shop.example.org", "www.example.com"]);

        Assert.Equal(["shop.example.org", "www.example.com", "edge.cdn.net"], names["203.0.113.10"]);
    }

    [Fact]
    public void Without_a_matching_query_the_map_order_is_kept_with_alias_heads_first()
    {
        var names = DnsNaming.ChooseNames(MapWithTwoAliases(), ["203.0.113.10"], ["unrelated.example.net"]);

        Assert.Equal(["shop.example.org", "www.example.com", "edge.cdn.net"], names["203.0.113.10"]);
    }

    [Fact]
    public void Query_matching_ignores_case_and_trailing_dot()
    {
        var names = DnsNaming.ChooseNames(MapWithTwoAliases(), ["203.0.113.10"], ["WWW.Example.com."]);

        Assert.Equal("www.example.com", names["203.0.113.10"][0]);
    }

    [Fact]
    public void Addresses_missing_from_the_cache_are_left_unnamed()
    {
        var names = DnsNaming.ChooseNames(MapWithTwoAliases(), ["198.51.100.1"], []);

        Assert.Empty(names);
    }
}
