namespace Taproom.Api.Clients;

/// <summary>Normalized view of one Omada client, independent of the raw Open API JSON shape.</summary>
public sealed record OmadaClientRecord
{
    public required string Mac { get; init; }
    public string? CustomName { get; init; }
    public string? Hostname { get; init; }
    public string? Ip { get; init; }
    public required bool Wireless { get; init; }
    public required bool Active { get; init; }
    public string? Ssid { get; init; }
    public string? ApName { get; init; }
    public string? Band { get; init; }
    public int? Rssi { get; init; }
    public long? RxBytes { get; init; }
    public long? TxBytes { get; init; }
    public DateTimeOffset? ConnectedSince { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
}

/// <summary>Normalized view of one OPNsense ARP table entry.</summary>
public sealed record ArpEntry
{
    public required string Mac { get; init; }
    public string? Ip { get; init; }
    public string? Hostname { get; init; }
    public required DateTimeOffset ObservedAt { get; init; }
}

/// <summary>Normalized view of one OPNsense DHCP lease, regardless of backend (dnsmasq/Kea/ISC).</summary>
public sealed record DhcpLease
{
    public required string Mac { get; init; }
    public string? Ip { get; init; }
    public string? Hostname { get; init; }
    public bool IsStatic { get; init; }
    public required DateTimeOffset ObservedAt { get; init; }
}
