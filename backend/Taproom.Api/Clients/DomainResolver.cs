using System.Net;
using DnsClient;
using DnsClient.Protocol;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Taproom.Api.Config;

namespace Taproom.Api.Clients;

/// <summary>
/// Re-resolves domains the client itself queried (rung 1 of the naming fallback) and does PTR reverse
/// lookups (rung 2), both against the OPNsense resolver so answers match what the device actually got.
/// </summary>
public sealed class DomainResolver
{
    private readonly LookupClient _dns;
    private readonly IMemoryCache _cache;

    public DomainResolver(IOptions<TaproomOptions> options, IMemoryCache cache)
    {
        var server = options.Value.Opnsense.DnsServer;
        _dns = new LookupClient(new LookupClientOptions(IPAddress.Parse(server))
        {
            Timeout = TimeSpan.FromSeconds(2),
            UseCache = false,
        });
        _cache = cache;
    }

    /// <summary>
    /// Resolves each domain (ordered most-recently-queried first) and returns, per IP, the distinct
    /// domains that resolved to it — in the same recency order, so the first entry is the best display name.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, List<string>>> ResolveDomainsAsync(
        IReadOnlyList<string> domainsMostRecentFirst, CancellationToken cancellationToken)
    {
        // Independent per-domain lookups (each usually a cache hit), so run them concurrently rather
        // than one at a time — this is on the hot path for a page that refreshes every few seconds.
        var resolved = await Task.WhenAll(domainsMostRecentFirst.Select(async domain => (domain, ips: await ResolveOneAsync(domain, cancellationToken))));

        var ipToDomains = new Dictionary<string, List<string>>();
        foreach (var (domain, ips) in resolved)
        {
            foreach (var ip in ips)
            {
                if (!ipToDomains.TryGetValue(ip, out var list))
                {
                    list = [];
                    ipToDomains[ip] = list;
                }
                if (!list.Contains(domain))
                {
                    list.Add(domain);
                }
            }
        }
        return ipToDomains;
    }

    private async Task<IReadOnlyList<string>> ResolveOneAsync(string domain, CancellationToken cancellationToken)
    {
        var cacheKey = $"fwd:{domain}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached))
        {
            return cached!;
        }

        try
        {
            var taskA = _dns.QueryAsync(domain, QueryType.A, cancellationToken: cancellationToken);
            var taskAaaa = _dns.QueryAsync(domain, QueryType.AAAA, cancellationToken: cancellationToken);
            await Task.WhenAll(taskA, taskAaaa);
            var resultA = taskA.Result;
            var resultAaaa = taskAaaa.Result;

            var ips = resultA.Answers.ARecords().Select(r => r.Address.ToString())
                .Concat(resultAaaa.Answers.AaaaRecords().Select(r => r.Address.ToString()))
                .Distinct()
                .ToList();

            var minTtl = resultA.Answers.Concat(resultAaaa.Answers)
                .Select(a => a.TimeToLive)
                .DefaultIfEmpty(300)
                .Min();
            var ttl = TimeSpan.FromSeconds(Math.Clamp(minTtl, 60, 600));

            IReadOnlyList<string> result = ips;
            _cache.Set(cacheKey, result, ttl);
            return result;
        }
        catch (Exception)
        {
            IReadOnlyList<string> empty = [];
            _cache.Set(cacheKey, empty, TimeSpan.FromSeconds(60));
            return empty;
        }
    }

    public async Task<string?> ReversePtrAsync(string ip, CancellationToken cancellationToken)
    {
        var cacheKey = $"ptr:{ip}";
        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(1));
            var result = await _dns.QueryReverseAsync(IPAddress.Parse(ip), cancellationToken: cts.Token);
            var ptr = result.Answers.PtrRecords().FirstOrDefault()?.PtrDomainName?.Value.TrimEnd('.');
            _cache.Set(cacheKey, ptr, TimeSpan.FromHours(1));
            return ptr;
        }
        catch (Exception)
        {
            _cache.Set(cacheKey, (string?)null, TimeSpan.FromHours(1));
            return null;
        }
    }
}
