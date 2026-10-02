namespace Taproom.Api.Clients;

/// <summary>Picks the display name for each remote IP from the cache map's candidates, using the query log only as a tiebreaker.</summary>
public static class DnsNaming
{
    private const int MaxNamesPerIp = 6;

    /// <summary>
    /// Returns, per IP, an ordered name list in the shape ConnectionEnricher's DNS rung expects (first is
    /// the display name, the rest are alternates). Candidates the caller recently queried come first, most
    /// recent first; remaining candidates keep the map's own order (CNAME-chain heads before intermediates).
    /// </summary>
    public static IReadOnlyDictionary<string, List<string>> ChooseNames(
        DnsCacheMap map, IEnumerable<string> remoteIps, IReadOnlyList<string> recentlyQueriedMostRecentFirst)
    {
        var recencyRank = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var domain in recentlyQueriedMostRecentFirst)
        {
            recencyRank.TryAdd(DnsCacheMap.NormalizeName(domain), recencyRank.Count);
        }

        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var ip in remoteIps)
        {
            if (!map.CandidatesByIp.TryGetValue(ip, out var candidates) || candidates.Count == 0) continue;

            var ordered = candidates
                .Select((name, index) => (name, index))
                .OrderBy(c => recencyRank.TryGetValue(c.name, out var rank) ? rank : int.MaxValue)
                .ThenBy(c => c.index)
                .Select(c => c.name)
                .Take(MaxNamesPerIp)
                .ToList();

            result[ip] = ordered;
        }
        return result;
    }
}
