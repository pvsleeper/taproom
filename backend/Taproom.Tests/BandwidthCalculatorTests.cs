using System.Text.Json;
using Taproom.Api.Clients;
using Taproom.Api.Sources.Opnsense;

namespace Taproom.Tests;

public class BandwidthCalculatorTests
{
    private static readonly IReadOnlyList<TopTalkerRecord> FixtureRecords = LoadFixtureRecords();

    private static IReadOnlyList<TopTalkerRecord> LoadFixtureRecords()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "top-talkers-example.json");
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parsed = JsonSerializer.Deserialize<Dictionary<string, OpnsenseTrafficTopResult>>(json, options)!;
        return BandwidthCalculator.MapRecords(parsed["lan"]);
    }

    [Fact]
    public void Maps_rate_bits_in_to_down_and_rate_bits_out_to_up()
    {
        // Confirmed against a live router: rate_bits_in is what grows during a download.
        var record = FixtureRecords.Single(r => r.Address == "10.0.1.179");
        Assert.Equal(556000, record.DownBps);
        Assert.Equal(9950, record.UpBps);
    }

    [Fact]
    public void Sums_rates_across_a_clients_ipv4_and_ipv6_addresses()
    {
        // 10.0.1.238 (ipv4) and the ipv6 record are treated as one dual-stack client for this test.
        var addresses = new HashSet<string> { "10.0.1.238", "2403:580a:ae97:0:294f:4497:74c7:1f51" };
        var (down, up) = BandwidthCalculator.Sum(FixtureRecords, addresses);

        Assert.Equal(416 + 0, down);
        Assert.Equal(320 + 576, up);
    }

    [Fact]
    public void Returns_zero_for_an_address_not_in_the_sample()
    {
        var (down, up) = BandwidthCalculator.Sum(FixtureRecords, new HashSet<string> { "10.0.1.99" });
        Assert.Equal(0, down);
        Assert.Equal(0, up);
    }

    [Fact]
    public void Ipv4_only_client_ignores_other_hosts_ipv6_traffic()
    {
        var (down, up) = BandwidthCalculator.Sum(FixtureRecords, new HashSet<string> { "10.0.1.203" });
        Assert.Equal(2410, down);
        Assert.Equal(288, up);
    }
}
