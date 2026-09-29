namespace Taproom.Api.Sources.Opnsense;

/// <summary>
/// Blocked and Failed are different things: Blocked means the resolver actively refused the query
/// (action isn't Pass, or a blocklist matched even on a Pass row — belt and suspenders, since a match
/// should always flip the action too). Failed means the query was allowed through but didn't resolve
/// (Pass with a non-NOERROR rcode, e.g. NXDOMAIN or SERVFAIL) — a lookup failure, not a block.
/// </summary>
public static class DnsQueryClassifier
{
    public static bool IsBlocked(string? action, string? blocklist) =>
        !string.Equals(action, "Pass", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(blocklist);

    public static bool IsFailed(string? action, string? blocklist, string? rcode) =>
        !IsBlocked(action, blocklist) && !string.Equals(rcode, "NOERROR", StringComparison.OrdinalIgnoreCase);
}
