namespace Taproom.Api.Clients;

public sealed record DeviceRate
{
    public required string Mac { get; init; }
    public required string Name { get; init; }
    public required long DownBps { get; init; }
    public required long UpBps { get; init; }
}

/// <summary>Groups the LAN top-talkers sample by device (summing a dual-stack client's IPv4+IPv6 rates), for the dashboard's busiest-devices tile.</summary>
public static class TopTalkersAggregator
{
    public static IReadOnlyList<DeviceRate> Aggregate(
        IReadOnlyList<TopTalkerRecord> records, IReadOnlyDictionary<string, AttributedDevice> addressMap, int limit)
    {
        var byMac = new Dictionary<string, (string Name, long Down, long Up)>();

        foreach (var record in records)
        {
            var device = DeviceAttributor.Attribute(addressMap, record.Address);
            var (name, down, up) = byMac.GetValueOrDefault(device.Mac, (device.Name, 0L, 0L));
            byMac[device.Mac] = (name, down + record.DownBps, up + record.UpBps);
        }

        return byMac
            .Select(kvp => new DeviceRate { Mac = kvp.Key, Name = kvp.Value.Name, DownBps = kvp.Value.Down, UpBps = kvp.Value.Up })
            .OrderByDescending(d => d.DownBps + d.UpBps)
            .Take(limit)
            .ToList();
    }
}
