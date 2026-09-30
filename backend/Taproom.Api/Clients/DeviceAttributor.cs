namespace Taproom.Api.Clients;

public sealed record AttributedDevice
{
    public required string Mac { get; init; }
    public required string Name { get; init; }
}

/// <summary>
/// Pure attribution of a raw source address to the device it belongs to, for the dashboard's network-wide
/// connection view. Built from the phase 2 client snapshot (IPv4) plus the NDP table (IPv6), so a dual-stack
/// client's IPv4 and IPv6 addresses both resolve to the same MAC. An address with no match becomes
/// "Unknown device", carrying the raw address so it's still identifiable in the UI.
/// </summary>
public static class DeviceAttributor
{
    public const string UnknownDeviceName = "Unknown device";

    public static IReadOnlyDictionary<string, AttributedDevice> BuildAddressMap(
        ClientSnapshot snapshot, IReadOnlyList<NdpEntry> ndp)
    {
        var map = new Dictionary<string, AttributedDevice>(StringComparer.Ordinal);

        var macToName = snapshot.Clients.ToDictionary(c => c.Mac, c => c.Name, StringComparer.Ordinal);

        foreach (var client in snapshot.Clients)
        {
            if (client.Ip is not null)
            {
                map[client.Ip] = new AttributedDevice { Mac = client.Mac, Name = client.Name };
            }
        }

        foreach (var entry in ndp)
        {
            if (macToName.TryGetValue(entry.Mac, out var name))
            {
                map[entry.Ipv6] = new AttributedDevice { Mac = entry.Mac, Name = name };
            }
        }

        return map;
    }

    public static AttributedDevice Attribute(IReadOnlyDictionary<string, AttributedDevice> addressMap, string address) =>
        addressMap.TryGetValue(address, out var device)
            ? device
            : new AttributedDevice { Mac = address, Name = $"{UnknownDeviceName} ({address})" };
}
