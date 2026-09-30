namespace Taproom.Api.Clients;

/// <summary>
/// Turns a network-wide (no client filter) DNS query log into the domain list ConnectionEnricher's rung-1
/// naming expects, so a device's own lookup names another device's raw connection to the same IP.
/// </summary>
public static class NetworkDnsNaming
{
    /// <summary>Distinct domains, most-recently-queried first (entries are already newest-first from the log).</summary>
    public static IReadOnlyList<string> ExtractDomainsMostRecentFirst(IReadOnlyList<DnsQueryEntry> entries) =>
        entries.Select(e => e.Domain).Distinct().ToList();
}
