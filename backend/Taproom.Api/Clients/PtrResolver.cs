using System.Net;
using DnsClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Taproom.Api.Config;

namespace Taproom.Api.Clients;

/// <summary>
/// PTR reverse lookups against the OPNsense resolver (rung 2 of the naming fallback). Every lookup made
/// here shows up in Unbound's log as a query from this host, so results are cached hard: a definitive
/// "no such name" (NXDOMAIN or an empty answer) for an hour, a found name for the caller's TTL, and only
/// a resolver error/timeout for a short while so it can recover.
/// </summary>
public sealed class PtrResolver
{
    private static readonly TimeSpan NegativeTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan ErrorTtl = TimeSpan.FromMinutes(5);

    private readonly LookupClient _dns;
    private readonly IMemoryCache _cache;

    public PtrResolver(IOptions<TaproomOptions> options, IMemoryCache cache)
    {
        var server = options.Value.Opnsense.DnsServer;
        _dns = new LookupClient(new LookupClientOptions(IPAddress.Parse(server))
        {
            Timeout = TimeSpan.FromSeconds(2),
            UseCache = false,
        });
        _cache = cache;
    }

    /// <summary>PTR fallback for naming a remote connection — a found name is cached for 1 hour.</summary>
    public Task<string?> ReversePtrAsync(string ip, CancellationToken cancellationToken) =>
        ReversePtrCoreAsync($"ptr:{ip}", ip, TimeSpan.FromHours(1), cancellationToken);

    /// <summary>
    /// PTR lookup for identifying which name OPNsense's Unbound log might use for one of THIS client's
    /// own addresses (its "client" field is sometimes an IP, sometimes a resolved name). A found name is
    /// cached for only 10 minutes since it needs to track the client's current identity.
    /// </summary>
    public Task<string?> ResolveClientNameAsync(string ip, CancellationToken cancellationToken) =>
        ReversePtrCoreAsync($"client-ptr:{ip}", ip, TimeSpan.FromMinutes(10), cancellationToken);

    private async Task<string?> ReversePtrCoreAsync(string cacheKey, string ip, TimeSpan foundTtl, CancellationToken cancellationToken)
    {
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

            var resolverError = result.HasError && result.Header.ResponseCode != DnsHeaderResponseCode.NotExistentDomain;
            _cache.Set(cacheKey, ptr, ptr is not null ? foundTtl : resolverError ? ErrorTtl : NegativeTtl);
            return ptr;
        }
        catch (Exception)
        {
            _cache.Set(cacheKey, (string?)null, ErrorTtl);
            return null;
        }
    }
}
