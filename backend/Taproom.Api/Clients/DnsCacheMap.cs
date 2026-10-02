using System.Net;

namespace Taproom.Api.Clients;

public sealed record DnsCacheRecord
{
    public required string Host { get; init; }
    public required string RrType { get; init; }
    public required string Value { get; init; }
    public int TtlSeconds { get; init; }
}

/// <summary>
/// IP → candidate domain names, built purely from Unbound's own resolver cache (no lookups of our own, so
/// nothing is added to the DNS log). For each address, every A/AAAA owner name is walked backwards along
/// CNAME aliases to the names a device would actually have asked for (e.g. www.example.com → edge.cdn.net
/// → 1.2.3.4 yields www.example.com). Candidates are ordered heads first, then intermediate/owner names.
/// </summary>
public sealed class DnsCacheMap
{
    private const int MaxChainNames = 200;
    private const int MaxCandidatesPerIp = 50;

    public static readonly DnsCacheMap Empty = new(new Dictionary<string, IReadOnlyList<string>>());

    public IReadOnlyDictionary<string, IReadOnlyList<string>> CandidatesByIp { get; }

    private DnsCacheMap(IReadOnlyDictionary<string, IReadOnlyList<string>> candidatesByIp)
    {
        CandidatesByIp = candidatesByIp;
    }

    public static string NormalizeName(string name) => name.TrimEnd('.').ToLowerInvariant();

    public static DnsCacheMap Build(IReadOnlyList<DnsCacheRecord> records)
    {
        var ownersByIp = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var aliasesByTarget = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var record in records)
        {
            if (record.TtlSeconds <= 0) continue;

            switch (record.RrType)
            {
                case "A" or "AAAA":
                    if (!IPAddress.TryParse(record.Value, out var address)) continue;
                    // Round-trip so IPv6 text matches the form connections report (compressed, lowercase).
                    var ip = address.ToString();
                    GetOrAdd(ownersByIp, ip).Add(NormalizeName(record.Host));
                    break;
                case "CNAME":
                    GetOrAdd(aliasesByTarget, NormalizeName(record.Value)).Add(NormalizeName(record.Host));
                    break;
            }
        }

        var candidatesByIp = new Dictionary<string, IReadOnlyList<string>>(ownersByIp.Count, StringComparer.Ordinal);
        foreach (var (ip, owners) in ownersByIp)
        {
            var heads = new SortedSet<string>(StringComparer.Ordinal);
            var others = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var owner in owners)
            {
                WalkBack(owner, aliasesByTarget, heads, others);
            }

            others.ExceptWith(heads);
            candidatesByIp[ip] = heads.Concat(others).Take(MaxCandidatesPerIp).ToList();
        }

        return new DnsCacheMap(candidatesByIp);
    }

    /// <summary>Breadth-first walk from a name back through every alias pointing at it; names nothing aliases to are heads.</summary>
    private static void WalkBack(
        string start,
        Dictionary<string, HashSet<string>> aliasesByTarget,
        SortedSet<string> heads,
        SortedSet<string> others)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { start };
        var queue = new Queue<string>();
        queue.Enqueue(start);

        while (queue.Count > 0 && visited.Count <= MaxChainNames)
        {
            var name = queue.Dequeue();
            if (aliasesByTarget.TryGetValue(name, out var aliases))
            {
                others.Add(name);
                foreach (var alias in aliases)
                {
                    if (visited.Add(alias)) queue.Enqueue(alias);
                }
            }
            else
            {
                heads.Add(name);
            }
        }
    }

    private static HashSet<string> GetOrAdd(Dictionary<string, HashSet<string>> map, string key)
    {
        if (!map.TryGetValue(key, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            map[key] = set;
        }
        return set;
    }
}
