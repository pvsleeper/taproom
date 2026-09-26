namespace Taproom.Api.Clients;

public enum ConnectionType
{
    Wireless,
    Wired,
}

public sealed record NetworkClient
{
    public required string Mac { get; init; }
    public required string Name { get; init; }
    public string? Hostname { get; init; }
    public string? Ip { get; init; }
    public required ConnectionType Connection { get; init; }
    public required bool Online { get; init; }
    public string? Ssid { get; init; }
    public string? ApName { get; init; }
    public string? Band { get; init; }
    public int? Rssi { get; init; }
    public long? RxBytes { get; init; }
    public long? TxBytes { get; init; }
    public DateTimeOffset? ConnectedSince { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
    public bool? IsStaticLease { get; init; }
    public required string[] Sources { get; init; }
}
