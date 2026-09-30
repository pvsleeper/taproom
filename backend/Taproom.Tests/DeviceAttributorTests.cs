using Taproom.Api.Clients;

namespace Taproom.Tests;

public class DeviceAttributorTests
{
    private static NetworkClient MakeClient(string mac, string name, string? ip) => new()
    {
        Mac = mac,
        Name = name,
        Ip = ip,
        Connection = ConnectionType.Wired,
        Online = true,
        LastSeen = DateTimeOffset.UtcNow,
        Sources = [],
    };

    [Fact]
    public void Ipv4_and_ipv6_addresses_attribute_to_the_same_mac()
    {
        var snapshot = new ClientSnapshot
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Sources = new Dictionary<string, SourceStatus>(),
            Clients = [MakeClient("AA:BB:CC:DD:EE:FF", "Living Room TV", "10.0.1.50")],
        };
        var ndp = new List<NdpEntry> { new() { Mac = "AA:BB:CC:DD:EE:FF", Ipv6 = "fe80::1" } };

        var map = DeviceAttributor.BuildAddressMap(snapshot, ndp);

        var fromIpv4 = DeviceAttributor.Attribute(map, "10.0.1.50");
        var fromIpv6 = DeviceAttributor.Attribute(map, "fe80::1");

        Assert.Equal("AA:BB:CC:DD:EE:FF", fromIpv4.Mac);
        Assert.Equal("AA:BB:CC:DD:EE:FF", fromIpv6.Mac);
        Assert.Equal("Living Room TV", fromIpv4.Name);
        Assert.Equal("Living Room TV", fromIpv6.Name);
    }

    [Fact]
    public void Unknown_address_becomes_unknown_device()
    {
        var snapshot = new ClientSnapshot
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Sources = new Dictionary<string, SourceStatus>(),
            Clients = [MakeClient("AA:BB:CC:DD:EE:FF", "Living Room TV", "10.0.1.50")],
        };
        var map = DeviceAttributor.BuildAddressMap(snapshot, []);

        var result = DeviceAttributor.Attribute(map, "10.0.1.99");

        Assert.Equal("10.0.1.99", result.Mac);
        Assert.Contains("Unknown device", result.Name);
        Assert.Contains("10.0.1.99", result.Name);
    }
}
