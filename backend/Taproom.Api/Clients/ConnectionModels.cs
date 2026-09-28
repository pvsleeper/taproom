namespace Taproom.Api.Clients;

/// <summary>One pre-NAT live connection from the OPNsense firewall state table.</summary>
public sealed record FirewallState
{
    public required string Protocol { get; init; }
    public required string RemoteIp { get; init; }
    public required int RemotePort { get; init; }
    public required string State { get; init; }
    public required long Bytes { get; init; }
    public required long Packets { get; init; }
    public required int AgeSeconds { get; init; }
}

/// <summary>One row from the Unbound DNS query log.</summary>
public sealed record DnsQueryEntry
{
    public required DateTimeOffset Time { get; init; }
    public required string Domain { get; init; }
    public required string Type { get; init; }
    public required bool Blocked { get; init; }
    public string? Blocklist { get; init; }
    public required string Rcode { get; init; }
}

public enum NameSource
{
    Dns,
    Ptr,
    Asn,
    Ip,
}

public sealed record GeoInfo
{
    public string? Country { get; init; }
    public string? City { get; init; }
    public double? Lat { get; init; }
    public double? Lon { get; init; }
    public long? Asn { get; init; }
    public string? Org { get; init; }
}

/// <summary>A resolved name for a remote IP, with the fallback rung it came from.</summary>
public sealed record NameResolution
{
    public required string Name { get; init; }
    public required NameSource Source { get; init; }
    public IReadOnlyList<string> OtherNames { get; init; } = [];
}

public sealed record EnrichedConnection
{
    public required string RemoteIp { get; init; }
    public required int RemotePort { get; init; }
    public required string Protocol { get; init; }
    public required string Name { get; init; }
    public required NameSource NameSource { get; init; }
    public IReadOnlyList<string> OtherNames { get; init; } = [];
    public string? Country { get; init; }
    public string? City { get; init; }
    public double? Lat { get; init; }
    public double? Lon { get; init; }
    public long? Asn { get; init; }
    public string? Org { get; init; }
    public required long Bytes { get; init; }
    public required long Packets { get; init; }
    public required int AgeSeconds { get; init; }
    public required string State { get; init; }
}

public sealed record ConnectionMarker
{
    public required string Key { get; init; }
    public required double Lat { get; init; }
    public required double Lon { get; init; }
    public required string Label { get; init; }
    public required int ConnectionCount { get; init; }
    public required long Bytes { get; init; }
}

public sealed record LocalConnection
{
    public required string RemoteIp { get; init; }
    public required int RemotePort { get; init; }
    public required string Protocol { get; init; }
    public required string Name { get; init; }
}

public sealed record HomeLocation
{
    public required double Lat { get; init; }
    public required double Lon { get; init; }
}

public sealed record ConnectionsResult
{
    public required string Mac { get; init; }
    public required string? Ip { get; init; }
    public required DateTimeOffset GeneratedAt { get; init; }
    public required HomeLocation Home { get; init; }
    public required IReadOnlyList<EnrichedConnection> Connections { get; init; }
    public required IReadOnlyList<ConnectionMarker> Markers { get; init; }
    public required IReadOnlyList<LocalConnection> Local { get; init; }
    public required IReadOnlyList<EnrichedConnection> UnknownLocation { get; init; }
    public required IReadOnlyList<string> Hints { get; init; }
    public required IReadOnlyDictionary<string, SourceStatus> Sources { get; init; }
}

public sealed record TopDomain
{
    public required string Domain { get; init; }
    public required int Count { get; init; }
    public required bool Blocked { get; init; }
}

public sealed record TopBlockedDomain
{
    public required string Domain { get; init; }
    public required int Count { get; init; }
    public required string Blocklist { get; init; }
}

public sealed record RecentQuery
{
    public required DateTimeOffset Time { get; init; }
    public required string Domain { get; init; }
    public required string Type { get; init; }
    public required string Action { get; init; }
    public required string Rcode { get; init; }
}

public sealed record DnsTotals
{
    public required int Queries { get; init; }
    public required int Blocked { get; init; }
}

public sealed record DnsResult
{
    public required int WindowMinutes { get; init; }
    public required DnsTotals Totals { get; init; }
    public required IReadOnlyList<TopDomain> TopDomains { get; init; }
    public required IReadOnlyList<TopBlockedDomain> TopBlocked { get; init; }
    public required IReadOnlyList<RecentQuery> Recent { get; init; }
}
