using Taproom.Api.Clients;

namespace Taproom.Tests;

public class TopTalkersAggregatorTests
{
    [Fact]
    public void Sums_a_clients_ipv4_and_ipv6_rates_into_one_device()
    {
        var addressMap = new Dictionary<string, AttributedDevice>
        {
            ["10.0.1.10"] = new AttributedDevice { Mac = "AA:AA", Name = "TV" },
            ["fe80::1"] = new AttributedDevice { Mac = "AA:AA", Name = "TV" },
        };
        var records = new[]
        {
            new TopTalkerRecord { Address = "10.0.1.10", DownBps = 1000, UpBps = 100 },
            new TopTalkerRecord { Address = "fe80::1", DownBps = 500, UpBps = 50 },
        };

        var result = TopTalkersAggregator.Aggregate(records, addressMap, limit: 5);

        var device = Assert.Single(result);
        Assert.Equal("AA:AA", device.Mac);
        Assert.Equal(1500, device.DownBps);
        Assert.Equal(150, device.UpBps);
    }

    [Fact]
    public void Orders_by_total_rate_and_respects_limit()
    {
        var addressMap = new Dictionary<string, AttributedDevice>
        {
            ["10.0.1.10"] = new AttributedDevice { Mac = "AA:AA", Name = "Busy" },
            ["10.0.1.20"] = new AttributedDevice { Mac = "BB:BB", Name = "Quiet" },
        };
        var records = new[]
        {
            new TopTalkerRecord { Address = "10.0.1.20", DownBps = 10, UpBps = 0 },
            new TopTalkerRecord { Address = "10.0.1.10", DownBps = 9000, UpBps = 0 },
        };

        var result = TopTalkersAggregator.Aggregate(records, addressMap, limit: 1);

        var device = Assert.Single(result);
        Assert.Equal("AA:AA", device.Mac);
    }
}
