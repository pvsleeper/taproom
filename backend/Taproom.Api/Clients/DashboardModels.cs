namespace Taproom.Api.Clients;

public sealed record ClientCounts
{
    public required int Online { get; init; }
    public required int Wireless { get; init; }
    public required int Wired { get; init; }
}

public sealed record DashboardDnsSummary
{
    public required int Queries { get; init; }
    public required int Blocked { get; init; }
    public required int Failed { get; init; }
}

public sealed record DashboardSummaryResult
{
    public required DateTimeOffset GeneratedAt { get; init; }
    public required ClientCounts Clients { get; init; }
    public required DashboardDnsSummary Dns { get; init; }
    public required IReadOnlyDictionary<string, SourceStatus> Sources { get; init; }
}

public sealed record DashboardWanResult
{
    public required DateTimeOffset SampledAt { get; init; }
    public required long DownBps { get; init; }
    public required long UpBps { get; init; }
    public required bool Ok { get; init; }
}

public sealed record DashboardConnectionsResult
{
    public required DateTimeOffset GeneratedAt { get; init; }
    public required HomeLocation Home { get; init; }
    public required IReadOnlyList<EnrichedConnection> Connections { get; init; }
    public required IReadOnlyList<ConnectionMarker> Markers { get; init; }
    public required IReadOnlyDictionary<string, SourceStatus> Sources { get; init; }
}

public sealed record TopTalkersResult
{
    public required DateTimeOffset SampledAt { get; init; }
    public required IReadOnlyList<DeviceRate> Devices { get; init; }
    public required bool Ok { get; init; }
}
