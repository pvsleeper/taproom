namespace Taproom.Api.Clients;

/// <summary>
/// Dashboard variant of <see cref="ConnectionEnricher"/>: groups by (device, remote ip, remote port) instead
/// of just (remote ip, port), so two devices talking to the same remote endpoint stay distinct connections
/// each attributed to its own device, and markers carry a per-device breakdown for the map's coloring/legend.
/// Reuses ConnectionEnricher's own naming fallback so the two views never disagree on what a remote IP is called.
/// </summary>
public static class NetworkWideConnectionEnricher
{
    public static EnrichmentOutcome Enrich(
        IReadOnlyList<FirewallState> states,
        IReadOnlyDictionary<string, List<string>> dnsNamesByIp,
        IReadOnlyDictionary<string, string?> ptrNamesByIp,
        IReadOnlyDictionary<string, GeoInfo> geoByIp,
        IReadOnlyDictionary<string, AttributedDevice> addressMap)
    {
        var publicGroups = new Dictionary<(string Mac, string Ip, int Port), List<FirewallState>>();

        foreach (var state in states)
        {
            if (state.SrcAddr is null || PrivateIp.IsPrivateOrLinkLocal(state.RemoteIp))
            {
                continue;
            }

            var device = DeviceAttributor.Attribute(addressMap, state.SrcAddr);
            var key = (device.Mac, state.RemoteIp, state.RemotePort);
            if (!publicGroups.TryGetValue(key, out var group))
            {
                group = [];
                publicGroups[key] = group;
            }
            group.Add(state);
        }

        var connections = new List<EnrichedConnection>();
        var unknownLocation = new List<EnrichedConnection>();

        foreach (var ((mac, ip, port), group) in publicGroups)
        {
            var device = DeviceAttributor.Attribute(addressMap, group[0].SrcAddr!);
            var (name, nameSource, otherNames) = ConnectionEnricher.ResolveName(ip, dnsNamesByIp, ptrNamesByIp, geoByIp);
            geoByIp.TryGetValue(ip, out var geo);

            var downBytes = group.Sum(s => s.DownBytes);
            var upBytes = group.Sum(s => s.UpBytes);

            var connection = new EnrichedConnection
            {
                RemoteIp = ip,
                RemotePort = port,
                Protocol = group[0].Protocol,
                Name = name,
                NameSource = nameSource,
                OtherNames = otherNames,
                Country = geo?.Country,
                City = geo?.City,
                Lat = geo?.Lat,
                Lon = geo?.Lon,
                Asn = geo?.Asn,
                Org = geo?.Org,
                Bytes = downBytes + upBytes,
                Packets = group.Sum(s => s.Packets),
                AgeSeconds = group.Max(s => s.AgeSeconds),
                State = group[0].State,
                DownBytes = downBytes,
                UpBytes = upBytes,
                Mac = device.Mac,
                DeviceName = device.Name,
            };

            if (connection.Lat is not null && connection.Lon is not null)
            {
                connections.Add(connection);
            }
            else
            {
                unknownLocation.Add(connection);
            }
        }

        var markers = BuildMarkers(connections);

        return new EnrichmentOutcome
        {
            Connections = connections,
            Markers = markers,
            Local = [],
            UnknownLocation = unknownLocation,
            Hints = [],
        };
    }

    private static IReadOnlyList<ConnectionMarker> BuildMarkers(IReadOnlyList<EnrichedConnection> connections)
    {
        var groups = new Dictionary<string, List<EnrichedConnection>>();
        foreach (var c in connections)
        {
            var key = ConnectionEnricher.MarkerKeyFor(c.Lat!.Value, c.Lon!.Value);
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }
            list.Add(c);
        }

        return groups.Select(kvp =>
        {
            var members = kvp.Value;
            var first = members[0];
            var label = string.IsNullOrEmpty(first.City)
                ? (first.Country ?? "Unknown")
                : $"{first.City}, {first.Country}";

            var devices = members
                .GroupBy(m => (m.Mac!, m.DeviceName!))
                .Select(g => new MarkerDeviceBreakdown
                {
                    Mac = g.Key.Item1,
                    Name = g.Key.Item2,
                    ConnectionCount = g.Count(),
                    DownBps = g.Sum(m => m.DownBps ?? 0),
                    UpBps = g.Sum(m => m.UpBps ?? 0),
                })
                .ToList();

            return new ConnectionMarker
            {
                Key = kvp.Key,
                Lat = members.Average(m => m.Lat!.Value),
                Lon = members.Average(m => m.Lon!.Value),
                Label = label,
                ConnectionCount = members.Count,
                Bytes = members.Sum(m => m.Bytes),
                Devices = devices,
            };
        }).ToList();
    }
}
