namespace Taproom.Api.Sources.Opnsense;

/// <summary>
/// The Unbound query log's "client" field is sometimes the client's IP and sometimes a resolved name
/// (e.g. a PTR or DHCP hostname) — which one depends on what OPNsense has cached for that device at log
/// time. A client's match set is therefore its IPs plus every name that resolves to them, all normalized
/// the same way, so filtering works regardless of which form a given row happens to use.
/// </summary>
public static class ClientMatching
{
    public static string Normalize(string value) => value.TrimEnd('.').ToLowerInvariant();

    public static bool Matches(string? client, IReadOnlySet<string> matchNames) =>
        client is not null && matchNames.Contains(Normalize(client));
}
