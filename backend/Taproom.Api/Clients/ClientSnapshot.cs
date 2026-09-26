namespace Taproom.Api.Clients;

public sealed record SourceStatus
{
    public required bool Ok { get; init; }
    public DateTimeOffset? LastSuccess { get; init; }
    public string? Error { get; init; }
}

public sealed record ClientSnapshot
{
    public required DateTimeOffset GeneratedAt { get; init; }
    public required IReadOnlyDictionary<string, SourceStatus> Sources { get; init; }
    public required IReadOnlyList<NetworkClient> Clients { get; init; }
}
