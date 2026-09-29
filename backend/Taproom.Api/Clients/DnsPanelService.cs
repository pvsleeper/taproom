using Microsoft.Extensions.Caching.Memory;
using Taproom.Api.Sources.Opnsense;

namespace Taproom.Api.Clients;

/// <summary>Orchestrates the DNS panel: fetches a client's Unbound query log and summarizes it, cached briefly.</summary>
public sealed class DnsPanelService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SourceTimeout = TimeSpan.FromSeconds(10);

    private readonly IUnboundLogClient _unboundLog;
    private readonly IOpnsenseClient _opnsense;
    private readonly ClientIdentityResolver _identityResolver;
    private readonly ClientSnapshotProvider _snapshotProvider;
    private readonly IMemoryCache _cache;

    public DnsPanelService(
        IUnboundLogClient unboundLog, IOpnsenseClient opnsense, ClientIdentityResolver identityResolver,
        ClientSnapshotProvider snapshotProvider, IMemoryCache cache)
    {
        _unboundLog = unboundLog;
        _opnsense = opnsense;
        _identityResolver = identityResolver;
        _snapshotProvider = snapshotProvider;
        _cache = cache;
    }

    public async Task<DnsResult?> GetDnsAsync(string mac, int minutes, int tail, CancellationToken cancellationToken)
    {
        var snapshot = await _snapshotProvider.GetSnapshotAsync(cancellationToken);
        var client = snapshot.Clients.FirstOrDefault(c => c.Mac == mac);
        if (client is null)
        {
            return null;
        }

        if (client.Ip is null)
        {
            return new DnsResult
            {
                WindowMinutes = minutes,
                Totals = new DnsTotals { Queries = 0, Blocked = 0 },
                TopDomains = [],
                TopBlocked = [],
                Recent = [],
            };
        }

        var cacheKey = $"dns:{mac}:{client.Ip}:{minutes}:{tail}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            using var cts = new CancellationTokenSource(SourceTimeout);
            var addresses = await ClientAddresses.GetAsync(_opnsense, mac, client.Ip, cts.Token);
            var identity = await _identityResolver.ResolveAsync(client.Ip, addresses, cts.Token);
            var queries = await _unboundLog.GetQueriesForClientAsync(identity, TimeSpan.FromMinutes(minutes), cts.Token);

            var byDomain = queries.GroupBy(q => q.Domain).ToList();

            var topDomains = byDomain
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new TopDomain { Domain = g.Key, Count = g.Count(), Blocked = g.All(q => q.Blocked) })
                .ToList();

            var topBlocked = queries
                .Where(q => q.Blocked)
                .GroupBy(q => q.Domain)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new TopBlockedDomain
                {
                    Domain = g.Key,
                    Count = g.Count(),
                    Blocklist = g.Select(q => q.Blocklist).FirstOrDefault(b => !string.IsNullOrEmpty(b)) ?? "",
                })
                .ToList();

            var recent = queries
                .OrderByDescending(q => q.Time)
                .Take(tail)
                .Select(q => new RecentQuery
                {
                    Time = q.Time,
                    Domain = q.Domain,
                    Type = q.Type,
                    Action = q.Blocked ? "block" : "pass",
                    Rcode = q.Rcode,
                })
                .ToList();

            return new DnsResult
            {
                WindowMinutes = minutes,
                Totals = new DnsTotals { Queries = queries.Count, Blocked = queries.Count(q => q.Blocked) },
                TopDomains = topDomains,
                TopBlocked = topBlocked,
                Recent = recent,
            };
        });
    }
}
