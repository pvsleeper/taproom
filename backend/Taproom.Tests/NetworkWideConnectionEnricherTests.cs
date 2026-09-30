using Taproom.Api.Clients;

namespace Taproom.Tests;

public class NetworkWideConnectionEnricherTests
{
    private static FirewallState State(string srcAddr, string remoteIp, int port = 443) => new()
    {
        SrcAddr = srcAddr,
        Protocol = "tcp",
        RemoteIp = remoteIp,
        RemotePort = port,
        State = "ESTABLISHED",
        DownBytes = 1000,
        UpBytes = 100,
        Packets = 10,
        AgeSeconds = 5,
    };

    private static readonly IReadOnlyDictionary<string, GeoInfo> Geo = new Dictionary<string, GeoInfo>
    {
        ["5.5.5.5"] = new GeoInfo { Country = "US", City = "Ashburn", Lat = 1, Lon = 1 },
    };

    [Fact]
    public void Two_devices_to_the_same_remote_stay_separate_connections_attributed_to_each_device()
    {
        var addressMap = new Dictionary<string, AttributedDevice>
        {
            ["10.0.1.10"] = new AttributedDevice { Mac = "AA:AA", Name = "TV" },
            ["10.0.1.20"] = new AttributedDevice { Mac = "BB:BB", Name = "Phone" },
        };
        var states = new[] { State("10.0.1.10", "5.5.5.5"), State("10.0.1.20", "5.5.5.5") };

        var outcome = NetworkWideConnectionEnricher.Enrich(
            states, new Dictionary<string, List<string>>(), new Dictionary<string, string?>(), Geo, addressMap);

        Assert.Equal(2, outcome.Connections.Count);
        Assert.Contains(outcome.Connections, c => c.Mac == "AA:AA" && c.DeviceName == "TV");
        Assert.Contains(outcome.Connections, c => c.Mac == "BB:BB" && c.DeviceName == "Phone");

        var marker = Assert.Single(outcome.Markers);
        Assert.Equal(2, marker.ConnectionCount);
        Assert.Equal(2, marker.Devices.Count);
        Assert.Contains(marker.Devices, d => d.Mac == "AA:AA" && d.ConnectionCount == 1);
        Assert.Contains(marker.Devices, d => d.Mac == "BB:BB" && d.ConnectionCount == 1);
    }

    [Fact]
    public void Unknown_source_address_becomes_unknown_device()
    {
        var addressMap = new Dictionary<string, AttributedDevice>();
        var states = new[] { State("10.0.1.99", "5.5.5.5") };

        var outcome = NetworkWideConnectionEnricher.Enrich(
            states, new Dictionary<string, List<string>>(), new Dictionary<string, string?>(), Geo, addressMap);

        var connection = Assert.Single(outcome.Connections);
        Assert.Equal("10.0.1.99", connection.Mac);
        Assert.Contains("Unknown device", connection.DeviceName);
    }
}
