namespace Taproom.Api.Clients;

public sealed record EnrichmentOutcome
{
    public required IReadOnlyList<EnrichedConnection> Connections { get; init; }
    public required IReadOnlyList<ConnectionMarker> Markers { get; init; }
    public required IReadOnlyList<LocalConnection> Local { get; init; }
    public required IReadOnlyList<EnrichedConnection> UnknownLocation { get; init; }
    public required IReadOnlyList<string> Hints { get; init; }
}

/// <summary>
/// Pure function that turns raw firewall states into named, located, grouped connections. All the
/// actual I/O (DNS resolution, PTR lookups, GeoIP) happens before this is called; it only combines
/// already-resolved lookups, so it's fully unit-testable with fixtures.
/// </summary>
public static class ConnectionEnricher
{
    public static EnrichmentOutcome Enrich(
        IReadOnlyList<FirewallState> states,
        IReadOnlyDictionary<string, List<string>> dnsNamesByIp,
        IReadOnlyDictionary<string, string?> ptrNamesByIp,
        IReadOnlyDictionary<string, GeoInfo> geoByIp,
        IReadOnlyDictionary<string, string> localNamesByIp,
        int dnsQueryCountInWindow)
    {
        var local = new List<LocalConnection>();
        var publicGroups = new Dictionary<(string Ip, int Port), List<FirewallState>>();

        foreach (var state in states)
        {
            if (PrivateIp.IsPrivateOrLinkLocal(state.RemoteIp))
            {
                local.Add(new LocalConnection
                {
                    RemoteIp = state.RemoteIp,
                    RemotePort = state.RemotePort,
                    Protocol = state.Protocol,
                    Name = localNamesByIp.GetValueOrDefault(state.RemoteIp, state.RemoteIp),
                });
                continue;
            }

            var key = (state.RemoteIp, state.RemotePort);
            if (!publicGroups.TryGetValue(key, out var group))
            {
                group = [];
                publicGroups[key] = group;
            }
            group.Add(state);
        }

        var connections = new List<EnrichedConnection>();
        var unknownLocation = new List<EnrichedConnection>();

        foreach (var ((ip, port), group) in publicGroups)
        {
            var (name, nameSource, otherNames) = ResolveName(ip, dnsNamesByIp, ptrNamesByIp, geoByIp);
            geoByIp.TryGetValue(ip, out var geo);

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
                Bytes = group.Sum(s => s.Bytes),
                Packets = group.Sum(s => s.Packets),
                AgeSeconds = group.Max(s => s.AgeSeconds),
                State = group[0].State,
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
        var hints = new List<string>();
        if (connections.Count >= 3 && connections.All(c => c.NameSource != NameSource.Dns) && dnsQueryCountInWindow == 0)
        {
            hints.Add("possibleOwnDns");
        }

        return new EnrichmentOutcome
        {
            Connections = connections,
            Markers = markers,
            Local = local,
            UnknownLocation = unknownLocation,
            Hints = hints,
        };
    }

    private static (string Name, NameSource Source, IReadOnlyList<string> OtherNames) ResolveName(
        string ip,
        IReadOnlyDictionary<string, List<string>> dnsNamesByIp,
        IReadOnlyDictionary<string, string?> ptrNamesByIp,
        IReadOnlyDictionary<string, GeoInfo> geoByIp)
    {
        if (dnsNamesByIp.TryGetValue(ip, out var domains) && domains.Count > 0)
        {
            return (domains[0], NameSource.Dns, domains.Skip(1).ToList());
        }

        if (ptrNamesByIp.TryGetValue(ip, out var ptr) && !string.IsNullOrEmpty(ptr))
        {
            return (ptr, NameSource.Ptr, []);
        }

        if (geoByIp.TryGetValue(ip, out var geo) && !string.IsNullOrEmpty(geo.Org))
        {
            return (geo.Org, NameSource.Asn, []);
        }

        return (ip, NameSource.Ip, []);
    }

    private static IReadOnlyList<ConnectionMarker> BuildMarkers(IReadOnlyList<EnrichedConnection> connections)
    {
        var groups = new Dictionary<string, List<EnrichedConnection>>();
        foreach (var c in connections)
        {
            // City-level grouping: round to 1 decimal degree (~11km), matching the spec's own example key.
            var key = $"{Math.Round(c.Lat!.Value, 1)},{Math.Round(c.Lon!.Value, 1)}";
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

            return new ConnectionMarker
            {
                Key = kvp.Key,
                Lat = members.Average(m => m.Lat!.Value),
                Lon = members.Average(m => m.Lon!.Value),
                Label = label,
                ConnectionCount = members.Count,
                Bytes = members.Sum(m => m.Bytes),
            };
        }).ToList();
    }
}
